using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Dagmay.RimWorld.Dialogue
{
    [DefOf]
    public static class MosaicDialogueLogEntryDefOf
    {
        public static LogEntryDef MosaicDialogue = null!;

        static MosaicDialogueLogEntryDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(MosaicDialogueLogEntryDefOf));
        }
    }

    public sealed class MosaicDialogueLogEntry : LogEntry
    {
        private string _text = string.Empty;
        private Pawn? _speaker;

        public MosaicDialogueLogEntry() : base(MosaicDialogueLogEntryDefOf.MosaicDialogue)
        {
        }

        public MosaicDialogueLogEntry(string text, Pawn? speaker)
            : base(MosaicDialogueLogEntryDefOf.MosaicDialogue)
        {
            _text = text ?? string.Empty;
            _speaker = speaker;
        }

        public override bool Concerns(Thing thing) => _speaker is not null && thing == _speaker;

        public override IEnumerable<Thing> GetConcerns() =>
            _speaker is null ? Enumerable.Empty<Thing>() : new Thing[] { _speaker };

        protected override string ToGameStringFromPOV_Worker(Thing pov, bool forceLog) => _text;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref _text, "mosaicDialogueText", string.Empty);
            Scribe_References.Look(ref _speaker, "mosaicDialogueSpeaker");
        }
    }
}
