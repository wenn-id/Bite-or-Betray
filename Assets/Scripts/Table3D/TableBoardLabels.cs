using System.Collections.Generic;
using BiteOrBetray.Core;
using UnityEngine;

namespace BiteOrBetray.Table3D
{
    /// <summary>
    /// Parlour chalkboard ("Table Info") and seat nameplate/heart-strip updates for the candy table.
    /// Keeps state-tuple guards so an idle frame allocates 0 B and never re-builds identical strings or meshes.
    /// </summary>
    public sealed class TableBoardLabels : MonoBehaviour
    {
        (int round, int candies, int good, int bad, int seconds) boardState;
        string boardText;
        TextMesh board;

        /// <summary>Board TextMesh created by the director in Setup(). May be null if Setup failed.</summary>
        public void Bind(TextMesh board) => this.board = board;

        /// <summary>Forget the cached board text so the next Refresh rebuilds it (language change, StopGame).</summary>
        public void InvalidateBoard() => boardText = null;

        /// <summary>Forget every seat's cached label text so the next Refresh rebuilds it (language change).</summary>
        internal void InvalidateSeats(IReadOnlyList<CandyTableDirector.Seat> seats)
        {
            foreach (var s in seats) s.labelText = null;
        }

        /// <summary>
        /// Refresh chalkboard + seat nameplates + 3D SugarHeartStrip states from the current table state.
        /// </summary>
        internal void Refresh(ITableState state, int displaySeconds, IReadOnlyList<CandyTableDirector.Seat> seats)
        {
            if (state == null || board == null) return;
            UpdateBoardText(state, displaySeconds);
            foreach (var s in seats)
            {
                var p = s.player;
                bool isTurn = p != null && state.Current == p && !state.IsOver;
                bool paralyzed = p != null && (p.Paralyzed || s.Paralyzed);
                bool cupcake = p != null && p.HasCupcake;
                bool eliminated = p != null && p.Eliminated;
                s.heartStrip?.UpdateState(p != null, p?.Hp ?? 0, state.Config.MaxHp, isTurn, cupcake, paralyzed, eliminated);

                if (s.label == null) continue;
                UpdateSeatText(state, s);
            }
        }

        void UpdateBoardText(ITableState state, int displaySeconds)
        {
            var tuple = (state.RoundNumber, state.CandiesInDispenser, state.GoodLeft, state.BadLeft, displaySeconds);
            if (boardText != null && boardState == tuple) return;
            boardState = tuple;
            // Coral chalk (#FF8B85 = 7.00:1 on #1B2426 dark slate chalkboard)
            string timer = boardState.seconds >= 0 ? $"\n<color=#ff8b85>{Strings.BoardTimer.Format(boardState.seconds)}</color>" : "";
            string text = Strings.TableBoard.Format(boardState.round, boardState.candies, boardState.good, boardState.bad) + timer;
            if (text == boardText) return;
            boardText = text;
            board.text = text;
        }

        void UpdateSeatText(ITableState state, CandyTableDirector.Seat s)
        {
            var p = s.player;
            var tuple = (s.name, p != null, p != null && p.Eliminated, p?.Hp ?? 0, state.Config.MaxHp,
                p?.Items.Count ?? 0, p != null && (p.Paralyzed || s.Paralyzed), p != null && p.HasCupcake,
                p != null && state.Current == p && !state.IsOver);
            // Compare values before interpolation so idle frames do not create new strings.
            if (s.labelText != null && s.labelState == tuple) return;
            s.labelState = tuple;
            string text = SeatText(state, s);
            if (text == s.labelText) return;
            s.labelText = text;
            s.label.text = text;
        }

        // Clean parlour nameplate: 3D SugarHeartStrip gems and physical pad items already display HP and inventory,
        // so the nameplate only needs the player's name plus an explicit text badge on KO/Paralyzed/Immune (never color alone).
        string SeatText(ITableState state, CandyTableDirector.Seat s)
        {
            var p = s.player;
            if (p == null) return $"<b>{s.name}</b>";
            if (p.Eliminated) return $"<b>{s.name}</b>  <color=#a81d1a><b>KO</b></color>";
            string turn = state.Current == p && !state.IsOver ? "· " : "";
            string status = (p.Paralyzed || s.Paralyzed ? $"  <color=#6b3fa0>{Strings.Paralyzed}</color>" : "")
                + (p.HasCupcake ? $"  <color=#1d5a8a>{Strings.Immune}</color>" : "");
            return $"<b>{turn}{s.name}</b>{status}";
        }
    }
}
