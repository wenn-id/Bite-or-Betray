using System.Collections.Generic;
using BiteOrBetray.Core;
using UnityEngine;

namespace BiteOrBetray.Table3D
{
    /// <summary>
    /// Board ("Table Info") and seat-label text for the candy table.
    ///
    /// Extracted from CandyTableDirector (H4) with no behaviour change. The director stays the
    /// coordinator: it owns the seats, the board TextMesh, the game state and the per-frame
    /// LateUpdate, and drives this component with one push call per frame. This component owns
    /// only the text-building and its state-tuple guards, so an idle frame still allocates
    /// nothing and never re-sets identical text (the M6/BOB-009 property the audit calls out).
    ///
    /// Wiring: the director adds this to the same GameObject (BoB_TableDirector) in Start,
    /// following the existing GameplayAudio / VRTableInput GetComponent-or-AddComponent
    /// pattern, then calls Bind(board) once Setup() has created the board TextMesh.
    /// No Unity lifecycle of its own (no Update/LateUpdate): the director calls Refresh from
    /// its LateUpdate, so there is no execution-order dependency to configure.
    ///
    /// Visibility note: the methods that take seats are `internal`, not `public`, because
    /// CandyTableDirector.Seat is an internal nested class; a public method may not expose
    /// a less-accessible type (CS0051). Same assembly (BiteOrBetray.Table3D), same access.
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
        /// Refresh board + seat-label text from the current table state. Called by the director
        /// from LateUpdate (once per frame, only when State != null). The per-seat label
        /// TextMesh is read live (s.label) because seat labels are created lazily by the
        /// director (RunGames / ApplyNetworkSeats) after Bind. No-ops when unbound.
        /// </summary>
        internal void Refresh(ITableState state, int displaySeconds, IReadOnlyList<CandyTableDirector.Seat> seats)
        {
            if (state == null || board == null) return;
            UpdateBoardText(state, displaySeconds);
            foreach (var s in seats)
            {
                if (s.label == null) continue;
                UpdateSeatText(state, s);
            }
        }

        void UpdateBoardText(ITableState state, int displaySeconds)
        {
            var tuple = (state.RoundNumber, state.CandiesInDispenser, state.GoodLeft, state.BadLeft, displaySeconds);
            if (boardText != null && boardState == tuple) return;
            boardState = tuple;
            string timer = boardState.seconds >= 0 ? $"\n<color=#c42e29>{Strings.BoardTimer.Format(boardState.seconds)}</color>" : "";
            string text = Strings.Board.Format(boardState.round, boardState.candies, boardState.good, boardState.bad) + timer;
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

        string SeatText(ITableState state, CandyTableDirector.Seat s)
        {
            var p = s.player;
            if (p == null) return $"<b>{s.name}</b>";
            if (p.Eliminated) return $"<b>{s.name}</b>\n<color=#c42e29>KO</color>";
            string status = (p.Paralyzed || s.Paralyzed ? $"\n<color=#8040a6>{Strings.Paralyzed}</color>" : "")
                + (p.HasCupcake ? $"\n<color=#2666a6>{Strings.Immune}</color>" : "");
            string turn = state.Current == p && !state.IsOver ? "> " : "";
            return $"<b>{turn}{s.name}</b>\n{Strings.SeatStatus.Format(p.Hp, state.Config.MaxHp, p.Items.Count)}{status}";
        }
    }
}
