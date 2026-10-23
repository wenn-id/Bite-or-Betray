using System.Collections;
using System.IO;
using System.Linq;
using BiteOrBetray.Core;
using BiteOrBetray.Table3D;
using BiteOrBetray.Table3D.Net;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.TestTools;

namespace BiteOrBetray.Tests.PlayMode
{
    /// <summary>
    /// BOB-063 disconnect timing on the host scene: the client drops while holding the candy, during its Little Pocket
    /// choice, during a bot's turn and on the results screen; a bot must take the seat and the match must reach a winner
    /// (or Play again) without exceptions. The host leaving in the lobby and mid-match must bring the host back to a
    /// working main menu and drop the client. The client scene side of a host leaving is OnlineHostLeaveTests.
    /// </summary>
    public partial class OnlineMatchTests
    {
        uint heldPoseSequence;

        GameObject HostCandy => Read<GameObject>(director, "candyObject");

        [UnityTest, Timeout(300000)]
        public IEnumerator Client_disconnect_while_holding_candy_returns_it_to_the_tray_and_bot_plays_on()
        {
            yield return StartOnlineTable();
            yield return UntilClientTurn("client turn to pick up the candy");
            // Real time and a slow bot, so the candy is checked before the bot starts animating it.
            Time.timeScale = 1;
            director.thinkSeconds = new Vector2(2, 2);
            var tray = Read<Vector3>(director, "trayPosition");
            float deadline = Time.realtimeSinceStartup + 10;
            while (HostCandy == null || Vector3.Distance(HostCandy.transform.position, tray) < .2f)
            {
                Assert.Less(Time.realtimeSinceStartup, deadline, "Host candy never followed the client's held pose.");
                SendHeldPose();
                yield return null;
            }
            SendHeldPose();
            yield return DisconnectClient("holding the candy");
            Assert.Less(Vector3.Distance(HostCandy.transform.position, tray), .01f,
                "Candy held by the disconnected client must return to the tray, not float at the seat the bot takes over.");
            yield return null;
            Assert.Less(Vector3.Distance(HostCandy.transform.position, tray), .01f, "Stale held poses must not move the candy again.");
            yield return FinishWithoutClient("holding the candy");
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator Client_disconnect_during_little_pocket_choice_bot_resolves_it()
        {
            yield return StartOnlineTable();
            yield return UntilClientTurn("client turn for Little Pocket");
            var player = Engine.Players.Single(p => p.ConfigIndex == clientConfig);
            Engine.DebugGiveItem(player, ItemType.LittlePocket);
            yield return Until(() => ClientSynced && clientTable.Latest.Current.Items.Contains(ItemType.LittlePocket), "client sees its Little Pocket");
            var s = clientTable.Latest;
            clientTable.Submit(new BotDecision { Kind = BotActionKind.UseItem, Index = IndexOf(s.Current.Items, t => t == ItemType.LittlePocket) }, s.CurrentSeat);
            yield return Until(() => Engine.Phase == GamePhase.AwaitingPocketChoice && Engine.Current == player && hostTable.InputOpen
                && ClientSynced && clientTable.Latest.Phase == GamePhase.AwaitingPocketChoice && clientTable.Latest.InputOpen,
                "client is choosing from Little Pocket");
            yield return DisconnectClient("Little Pocket choice");
            yield return Until(() => Engine.Phase != GamePhase.AwaitingPocketChoice || Engine.Current != player,
                "bot resolves the disconnected client's Little Pocket choice");
            yield return FinishWithoutClient("Little Pocket choice");
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator Client_disconnect_during_bot_turn_game_continues_to_winner()
        {
            yield return StartOnlineTable();
            float deadline = Time.realtimeSinceStartup + 180;
            while (true)
            {
                Assert.Less(Time.realtimeSinceStartup, deadline, "No bot turn after the client acted.");
                Assert.IsFalse(Engine.IsOver, "Game ended before a bot turn after the client acted.");
                bool acted = Engine.LogLines.Any(line => IsActionOf(line, ClientName));
                var current = Engine.Current;
                if (acted && hostTable.Authority.Seats[current.ConfigIndex].Controller == SeatController.Bot) break;
                Step();
                yield return null;
            }
            yield return DisconnectClient("bot turn");
            yield return FinishWithoutClient("bot turn");
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator Client_disconnect_on_results_screen_play_again_seats_a_bot()
        {
            yield return StartOnlineTable();
            float deadline = Time.realtimeSinceStartup + 180;
            while (!ResultShown || !ClientSynced)
            {
                Assert.Less(Time.realtimeSinceStartup, deadline, $"Online game stuck: {Engine.Phase}, turn {Engine.TurnNumber}");
                Step();
                yield return null;
            }
            Assert.IsTrue(clientTable.Latest.IsOver);
            yield return DisconnectClient("results screen");
            Assert.IsTrue(ResultShown, "Results stay on the table after the client leaves.");
            var previous = Engine;
            Select(Button("Play again"));
            yield return Until(() => Engine != null && Engine != previous, "Play again after the client left");
            Assert.AreEqual(SeatController.Bot, hostTable.Authority.Seats[clientConfig].Controller, "The new game seats a bot for the client.");
            Assert.IsTrue(Engine.Players.Single(p => p.ConfigIndex == clientConfig).IsBot);
            yield return FinishWithoutClient("Play again after results");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator Host_leaving_lobby_returns_host_to_menu_and_drops_client()
        {
            Assert.IsNull(Engine, "Still in the room: no table started.");
            yield return HostLeaves("lobby");
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator Host_leaving_mid_match_returns_host_to_menu_and_drops_client()
        {
            yield return StartOnlineTable();
            float deadline = Time.realtimeSinceStartup + 120;
            while (!Engine.LogLines.Any(line => IsActionOf(line, ClientName)))
            {
                Assert.Less(Time.realtimeSinceStartup, deadline, "Client never acted.");
                Assert.IsFalse(Engine.IsOver, "Game ended before the host could leave.");
                Step();
                yield return null;
            }
            yield return HostLeaves("mid-match");
        }

        bool ClientSynced => clientTable.Latest != null && hostTable.Authority != null
            && clientTable.Latest.GameId == hostTable.Authority.GameId && clientTable.Latest.Revision == hostTable.Authority.Revision;

        // The host human eats on its turns; the client never acts, so its first turn opens with the candy still on the tray.
        IEnumerator UntilClientTurn(string message)
        {
            float deadline = Time.realtimeSinceStartup + 120;
            while (!(hostTable.InputOpen && Engine.Phase == GamePhase.AwaitingAction && Engine.Current.ConfigIndex == clientConfig
                       && ClientSynced && clientTable.Latest.InputOpen))
            {
                Assert.Less(Time.realtimeSinceStartup, deadline, message);
                Assert.IsFalse(Engine.IsOver, message + ": game ended first");
                Assert.IsFalse(Engine.Players.Single(p => p.ConfigIndex == clientConfig).Eliminated, message + ": client knocked out first");
                if (HostReady) Select(Candy);
                yield return null;
            }
        }

        // The client's pose RPC as LocalAvatarTracking sends it: left hand tracked and holding the candy 1 m above its seat.
        void SendHeldPose()
        {
            var s = clientTable.Latest;
            var hand = new Vector3(0, 1, 0);
            var pose = new AvatarPose
            {
                head = new Pose(new Vector3(0, 1.2f, 0), Quaternion.identity),
                leftHand = new Pose(hand, Quaternion.identity),
                rightHand = new Pose(new Vector3(.2f, .9f, .2f), Quaternion.identity),
                headTracked = true,
                leftTracked = true,
                rightTracked = true,
                leftGrip = 1,
                sequence = ++heldPoseSequence
            };
            using var stream = new MemoryStream(80);
            using var writer = new BinaryWriter(stream);
            writer.Write(s.GameId); writer.Write(s.Revision);
            AvatarVisualWire.WritePose(writer, pose);
            writer.Write(true); AvatarVisualWire.WriteVector(writer, hand + Vector3.forward * .06f);
            Invoke(clientTable, "SubmitPoseRpc", stream.ToArray(), default(RpcParams));
        }

        IEnumerator DisconnectClient(string moment)
        {
            var player = Engine.Players.Single(p => p.ConfigIndex == clientConfig);
            client.Shutdown();
            yield return Until(() => hostTable.Authority.Seats[clientConfig].Controller == SeatController.Bot,
                moment + ": host replaces the client seat with a bot");
            Assert.IsTrue(player.IsBot, moment + ": engine player now played by a bot");
            Assert.IsFalse(hostTable.IsOnlineSeat(clientConfig), moment);
        }

        IEnumerator FinishWithoutClient(string moment)
        {
            Time.timeScale = 20;
            float deadline = Time.realtimeSinceStartup + 180;
            while (!ResultShown)
            {
                Assert.Less(Time.realtimeSinceStartup, deadline, $"{moment}: game stuck after client disconnect: {Engine.Phase}, turn {Engine.TurnNumber}");
                if (HostReady) Select(Candy);
                yield return null;
            }
            Assert.IsTrue(Engine.IsOver, moment);
            Assert.IsNotNull(Engine.Winner, moment);
            Assert.IsFalse(client.IsListening, moment);
            Assert.AreEqual(1, host.ConnectedClientsIds.Count, moment + ": only the host remains");
        }

        IEnumerator HostLeaves(string moment)
        {
            host.Shutdown();
            yield return Until(() => !host.IsListening && !host.ShutdownInProgress, moment + ": host stopped");
            yield return Until(() => director.Network == null, moment + ": host table detached from the director");
            // A client without a disconnect packet notices only after the transport timeout (30 s by default).
            yield return Until(() => !client.IsListening && !client.ShutdownInProgress, moment + ": client dropped after the host left", 45);
            Assert.IsTrue(clientTable == null || clientTable.Latest == null, moment + ": client table cleared");
            Assert.IsNull(Read<GameEngine>(director, "engine"), moment + ": host game stopped");
            var play = Button("Play vs Bots");
            Assert.IsTrue(play.gameObject.activeInHierarchy && play.interactable, moment + ": host back at the main menu");
            Select(play);
            yield return null;
            Assert.IsTrue(Button("Start").interactable, moment + ": host input still opens bot setup");
        }

        TableSelectable Button(string label) => director.GetComponentsInChildren<TableSelectable>(true)
            .Single(t => t.name == "Button " + label);
    }
}
