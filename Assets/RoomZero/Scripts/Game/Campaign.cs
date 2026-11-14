// The Harvest Formula campaign: three rooms played one after another.
//   Room 1  SEED VAULT LAB          TREASURE_HUNT  find the formula fragments, open the seed vault
//   Room 2  HYDROPONICS GREENHOUSE  GREENHOUSE     mix the nutrient feed, balance the grow lights
//   Room 3  GENE LAB                GENE_LAB       tag healthy cells, splice the drought-proof gene
// Owner: #6 Game Systems.

using RoomZero.Spatial;

namespace RoomZero.Game
{
    public static class Campaign
    {
        public static readonly MissionTemplateId[] Rooms = { MissionTemplateId.TREASURE_HUNT, MissionTemplateId.GREENHOUSE, MissionTemplateId.GENE_LAB };

        /// 1-based room number, or 0 if the template isn't part of the campaign (the classic heists).
        public static int RoomNumber(MissionTemplateId t) { int i = System.Array.IndexOf(Rooms, t); return i < 0 ? 0 : i + 1; }

        public static bool IsCampaign(MissionTemplateId t) => RoomNumber(t) > 0;
        public static bool IsFinal(MissionTemplateId t) => RoomNumber(t) == Rooms.Length;

        public static bool TryNext(MissionTemplateId t, out MissionTemplateId next)
        {
            int n = RoomNumber(t);
            next = n > 0 && n < Rooms.Length ? Rooms[n] : t;
            return n > 0 && n < Rooms.Length;
        }

        public static string RoomName(MissionTemplateId t)
        {
            switch (t)
            {
                case MissionTemplateId.TREASURE_HUNT: return "SEED VAULT LAB";
                case MissionTemplateId.GREENHOUSE: return "HYDROPONICS GREENHOUSE";
                case MissionTemplateId.GENE_LAB: return "GENE LAB";
                default: return t.ToString().Replace('_', ' ');
            }
        }

        public static LabTheme Theme(MissionTemplateId t) =>
            t == MissionTemplateId.GREENHOUSE ? LabTheme.Greenhouse : t == MissionTemplateId.GENE_LAB ? LabTheme.GeneLab : LabTheme.VaultLab;

        public static string AshaIntro(MissionTemplateId t)
        {
            switch (t)
            {
                case MissionTemplateId.TREASURE_HUNT: return "The formula fragments are hidden somewhere in this lab. Let's find them together.";
                case MissionTemplateId.GREENHOUSE: return "Greenhouse time. The seed needs the right feed and the right light. Let's grow it.";
                case MissionTemplateId.GENE_LAB: return "Last room. We find healthy cells, then we repair the crop's gene.";
                default: return "Dr. Asha here. I'll guide you through this one.";
            }
        }

        public static string WinLine(MissionTemplateId t)
        {
            switch (t)
            {
                case MissionTemplateId.TREASURE_HUNT: return "Room 1 cleared: the Harvest Formula seed is safe.";
                case MissionTemplateId.GREENHOUSE: return "Room 2 cleared: the protein crop is growing.";
                case MissionTemplateId.GENE_LAB: return "All 3 rooms cleared: a drought-proof, high-protein crop for the kids who need it.";
                default: return "Mission complete, agent. ZERO is impressed.";
            }
        }
    }
}
