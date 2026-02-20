using System.Collections.Generic;

public static class DistrictData
{
    public struct SceneEntry
    {
        public string sceneName;
        public string displayTitle;
        public string episodeLabel;
    }

    public struct DistrictDef
    {
        public string districtId;
        public string nameZH;
        public SceneEntry[] scenes;
    }

    public static readonly Dictionary<string, DistrictDef> Districts = new Dictionary<string, DistrictDef>
    {
        // -- Hong Kong Island --------------------------------------------------

        { "central_western", new DistrictDef {
            districtId = "central_western",
            nameZH     = "中西區",
            scenes     = new SceneEntry[] {
                new SceneEntry { sceneName = "Angel", displayTitle = "Angel放假", episodeLabel = "" }
            }
        }},

        { "eastern", new DistrictDef {
            districtId = "eastern",
            nameZH     = "東區",
            scenes     = new SceneEntry[0]
        }},

        { "southern", new DistrictDef {
            districtId = "southern",
            nameZH     = "南區",
            scenes     = new SceneEntry[0]
        }},

        { "wan_chai", new DistrictDef {
            districtId = "wan_chai",
            nameZH     = "灣仔區",
            scenes     = new SceneEntry[0]
        }},

        // -- Kowloon -----------------------------------------------------------

        { "kowloon_city", new DistrictDef {
            districtId = "kowloon_city",
            nameZH     = "九龍城區",
            scenes     = new SceneEntry[0]
        }},

        { "kwun_tong", new DistrictDef {
            districtId = "kwun_tong",
            nameZH     = "觀塘區",
            scenes     = new SceneEntry[0]
        }},

        { "sham_shui_po", new DistrictDef {
            districtId = "sham_shui_po",
            nameZH     = "深水埗區",
            scenes     = new SceneEntry[0]
        }},

        { "wong_tai_sin", new DistrictDef {
            districtId = "wong_tai_sin",
            nameZH     = "黃大仙區",
            scenes     = new SceneEntry[0]
        }},

        { "yau_tsim_mong", new DistrictDef {
            districtId = "yau_tsim_mong",
            nameZH     = "油尖旺區",
            scenes     = new SceneEntry[0]
        }},

        // -- New Territories ---------------------------------------------------

        { "islands", new DistrictDef {
            districtId = "islands",
            nameZH     = "離島區",
            scenes     = new SceneEntry[0]
        }},

        { "kwai_tsing", new DistrictDef {
            districtId = "kwai_tsing",
            nameZH     = "葵青區",
            scenes     = new SceneEntry[0]
        }},

        { "north", new DistrictDef {
            districtId = "north",
            nameZH     = "北區",
            scenes     = new SceneEntry[0]
        }},

        { "sai_kung", new DistrictDef {
            districtId = "sai_kung",
            nameZH     = "西貢區",
            scenes     = new SceneEntry[0]
        }},

        { "sha_tin", new DistrictDef {
            districtId = "sha_tin",
            nameZH     = "沙田區",
            scenes     = new SceneEntry[0]
        }},

        { "tai_po", new DistrictDef {
            districtId = "tai_po",
            nameZH     = "大埔區",
            scenes     = new SceneEntry[0]
        }},

        { "tsuen_wan", new DistrictDef {
            districtId = "tsuen_wan",
            nameZH     = "荃灣區",
            scenes     = new SceneEntry[0]
        }},

        { "tuen_mun", new DistrictDef {
            districtId = "tuen_mun",
            nameZH     = "屯門區",
            scenes     = new SceneEntry[0]
        }},

        { "yuen_long", new DistrictDef {
            districtId = "yuen_long",
            nameZH     = "元朗區",
            scenes     = new SceneEntry[0]
        }},
    };
}
