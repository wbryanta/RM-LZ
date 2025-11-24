using RimWorld;
using UnityEngine;
using Verse;

namespace LandingZone.Core.UI
{
    /// <summary>
    /// Simple dialog that displays a preset token and lets the user copy it.
    /// </summary>
    public class Dialog_ShowToken : Window
    {
        private readonly string _presetName;
        private readonly string _token;

        public Dialog_ShowToken(string presetName, string token)
        {
            _presetName = presetName;
            _token = token;

            doCloseButton = false; // Only use X button to avoid overlap
            doCloseX = true;
            absorbInputAroundWindow = true;
            forcePause = true;
        }

        public override Vector2 InitialSize => new Vector2(700f, 260f);

        public override void DoWindowContents(Rect inRect)
        {
            var listing = new Listing_Standard();
            listing.Begin(inRect);

            Text.Font = GameFont.Medium;
            listing.Label("LandingZone_TokenExportedTitle".Translate(_presetName));
            Text.Font = GameFont.Small;
            listing.GapLine();
            listing.Gap(6f);

            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.8f, 0.8f, 0.8f);
            listing.Label("LandingZone_TokenExportedInstructions".Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            listing.Gap(8f);

            // Token display (read-only)
            listing.Label("LandingZone_TokenValueLabel".Translate());
            Rect textRect = listing.GetRect(80f);
            Widgets.TextArea(textRect, _token);

            listing.Gap(8f);

            if (listing.ButtonText("LandingZone_CopyToken".Translate()))
            {
                GUIUtility.systemCopyBuffer = _token;
                Messages.Message("LandingZone_TokenCopied".Translate(_presetName), MessageTypeDefOf.NeutralEvent, false);
            }

            listing.End();
        }
    }
}
