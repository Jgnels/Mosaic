using System;
using System.Linq;
using Dagmay.Core.Contracts;
using Dagmay.RimWorld.Persistence;
using global::RimWorld;
using UnityEngine;
using Verse;

namespace Dagmay.RimWorld.Dialogue
{
    /// <summary>
    /// Read-only participant-scoped view over verified admitted dialogue events.
    /// This window has no mutation, provider, scheduling, or gameplay authority.
    /// </summary>
    public sealed class MosaicConversationHistoryWindow : MainTabWindow
    {
        private const float ParticipantPaneWidth = 250f;
        private const float Gap = 12f;
        private const float RowPadding = 8f;

        private Vector2 _participantScroll;
        private Vector2 _historyScroll;
        private IndividualId? _selectedIndividualId;

        public override Vector2 InitialSize => new Vector2(1100f, 720f);

        public override void PreOpen()
        {
            base.PreOpen();
            RefreshSelection();
        }

        public override void DoWindowContents(Rect inRect)
        {
            var component = DagmayIdentityGameComponent.Current;
            if (component is null)
            {
                Widgets.Label(inRect, "Mosaic conversation history is unavailable outside an active game.");
                return;
            }

            var data = component.BuildConversationHistoryData(_selectedIndividualId, 100);
            if (!_selectedIndividualId.HasValue && data.Participants.Count > 0)
            {
                _selectedIndividualId = data.Participants[0].IndividualId;
                data = component.BuildConversationHistoryData(_selectedIndividualId, 100);
            }

            var left = new Rect(inRect.x, inRect.y, ParticipantPaneWidth, inRect.height);
            var right = new Rect(
                left.xMax + Gap,
                inRect.y,
                inRect.width - ParticipantPaneWidth - Gap,
                inRect.height);

            DrawParticipants(left, data);
            DrawHistory(right, data);
        }

        private void DrawParticipants(Rect rect, RimWorldConversationHistoryData data)
        {
            Widgets.DrawMenuSection(rect);
            var inner = rect.ContractedBy(8f);
            var header = new Rect(inner.x, inner.y, inner.width, 32f);
            Text.Font = GameFont.Medium;
            Widgets.Label(header, "Colonists");
            Text.Font = GameFont.Small;

            var viewport = new Rect(
                inner.x,
                header.yMax + 6f,
                inner.width,
                Math.Max(inner.height - header.height - 6f, 1f));
            var contentHeight = Math.Max(
                viewport.height,
                data.Participants.Count * 38f);
            var viewRect = new Rect(0f, 0f, viewport.width - 16f, contentHeight);

            Widgets.BeginScrollView(viewport, ref _participantScroll, viewRect);
            var y = 0f;
            foreach (var participant in data.Participants)
            {
                var row = new Rect(0f, y, viewRect.width, 34f);
                var selected = _selectedIndividualId.HasValue &&
                    _selectedIndividualId.Value == participant.IndividualId;
                if (selected) Widgets.DrawHighlightSelected(row);
                else if (Mouse.IsOver(row)) Widgets.DrawHighlight(row);

                if (Widgets.ButtonInvisible(row))
                {
                    _selectedIndividualId = participant.IndividualId;
                    _historyScroll = Vector2.zero;
                }

                Widgets.Label(row.ContractedBy(6f, 4f), participant.DisplayLabel);
                y += 38f;
            }
            Widgets.EndScrollView();
        }

        private void DrawHistory(Rect rect, RimWorldConversationHistoryData data)
        {
            Widgets.DrawMenuSection(rect);
            var inner = rect.ContractedBy(10f);
            var selected = data.SelectedHistory;

            Text.Font = GameFont.Medium;
            Widgets.Label(
                new Rect(inner.x, inner.y, inner.width, 32f),
                selected is null
                    ? "Conversation History"
                    : selected.ViewerLabel + " — Conversation History");
            Text.Font = GameFont.Small;

            var diagnosticY = inner.y + 34f;
            if (!string.IsNullOrWhiteSpace(data.Diagnostic))
            {
                GUI.color = Color.gray;
                Widgets.Label(
                    new Rect(inner.x, diagnosticY, inner.width, 24f),
                    data.Diagnostic);
                GUI.color = Color.white;
            }

            var body = new Rect(
                inner.x,
                diagnosticY + 28f,
                inner.width,
                Math.Max(inner.height - 62f, 1f));

            if (selected is null || selected.Rows.Count == 0)
            {
                Widgets.Label(
                    body,
                    "No verified displayed conversations are available for this colonist yet.");
                return;
            }

            var heights = selected.Rows
                .Select(row => RowHeight(body.width - 24f, row))
                .ToArray();
            var contentHeight = Math.Max(
                body.height,
                heights.Sum() + Math.Max(0, heights.Length - 1) * 8f);
            var viewRect = new Rect(0f, 0f, body.width - 16f, contentHeight);

            Widgets.BeginScrollView(body, ref _historyScroll, viewRect);
            var y = 0f;
            for (var index = 0; index < selected.Rows.Count; index++)
            {
                var row = selected.Rows[index];
                var height = heights[index];
                var rowRect = new Rect(0f, y, viewRect.width, height);
                Widgets.DrawMenuSection(rowRect);

                var content = rowRect.ContractedBy(RowPadding);
                Text.Font = GameFont.Small;
                Widgets.Label(
                    new Rect(content.x, content.y, content.width, 24f),
                    row.SpeakerLabel + " → " + row.RecipientLabel);

                GUI.color = Color.gray;
                Widgets.Label(
                    new Rect(content.x, content.y + 22f, content.width, 22f),
                    "Tick " + row.DisplayedAtTick
                    + " • " + row.Channel
                    + " • evidence " + row.SupportingEvidenceCount);
                GUI.color = Color.white;

                Widgets.Label(
                    new Rect(
                        content.x,
                        content.y + 46f,
                        content.width,
                        Math.Max(content.height - 46f, 24f)),
                    row.Text);

                y += height + 8f;
            }
            Widgets.EndScrollView();
        }

        private static float RowHeight(
            float width,
            Dagmay.Core.Dialogue.ConversationHistoryViewerRow row)
        {
            var textHeight = Text.CalcHeight(row.Text, Math.Max(width - RowPadding * 2f, 80f));
            return Math.Max(92f, textHeight + 66f);
        }

        private void RefreshSelection()
        {
            var component = DagmayIdentityGameComponent.Current;
            if (component is null)
            {
                _selectedIndividualId = null;
                return;
            }

            var data = component.BuildConversationHistoryData(_selectedIndividualId, 100);
            if (_selectedIndividualId.HasValue &&
                data.Participants.Any(value =>
                    value.IndividualId == _selectedIndividualId.Value))
            {
                return;
            }

            _selectedIndividualId = data.Participants.Count > 0
                ? data.Participants[0].IndividualId
                : (IndividualId?)null;
        }
    }
}
