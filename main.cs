using BepInEx;
using ClassesManagerReborn.Util;
using HarmonyLib;
using ModdingUtils.Utils;
using RarityLib;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using UnboundLib;
using UnboundLib.Cards;
using UnboundLib.Utils;
using UnityEngine;
using WillsWackyManagers.Utils;
using static UnityEngine.Random;
using UnityEngine.UI;
using Photon.Pun;
using System.Collections;
using System.Threading;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using System.Timers;

namespace CardDataDumper
{
    // Declares our Mod to Bepin
    [BepInPlugin(ModId, ModName, Version)]
    // The game our Mod Is associated with
    [BepInProcess("Rounds.exe")]
    public class main : BaseUnityPlugin
    {
        private const string ModId = "com.Root.Dump";
        private const string ModName = "Card Data Dumper";
        public const string Version = "0.0.0";
        public const string Databse = "Cards.sqlite";
        public const string BaseURL = "https://rounds.thunderstore.io/package/";
        public List<string> ProsessedCards = new List<string>();
        public Dictionary<string,string> modurls = new Dictionary<string,string>();
        public static Dictionary<AssetBundle, Assembly> BundleLocations = new Dictionary<AssetBundle, Assembly>();
        //SQLiteConnection m_dbConnection;
        string CardData = "{\"Cards\": [";
        string StatsData = "{\"Stats\": [";
        string ThemeData = "{\"Theme\": [";
        string MapsData = "{\"Maps\": [";
        GameObject camObj, lighObj;
        Camera camera, lightCam;
        int renderWidth = 350;
        int renderHeight = 500;
        int renderFrames = 10;

        internal static List<CardInfo> allCards
        {
            get
            {
                List<CardInfo> list = new List<CardInfo>();
                list.AddRange((ObservableCollection<CardInfo>)typeof(CardManager).GetField("activeCards", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).GetValue(null));
                list.AddRange((List<CardInfo>)typeof(CardManager).GetField("inactiveCards", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).GetValue(null));
                list.Sort((CardInfo x, CardInfo y) => string.CompareOrdinal(x.gameObject.name, y.gameObject.name));
                return list;
            }
        }
        void Start()
        {
            /*SQLiteConnection.CreateFile(Databse); 
            m_dbConnection = new SQLiteConnection("Data Source=MyDatabase.sqlite;Version=3;");
            m_dbConnection.Open();
            new SQLiteCommand("CREATE TABLE Cards (ID VARCHAR NOT NULL PRIMARY KEY,Name VARCHAR NOT NULL,Rarity VARCHAR NOT NULL,Theme VARCHAR NOT NULL,Descripion VARCHAR NOT NULL,IsCurse BOOLEAN NOT NULL)", m_dbConnection).ExecuteNonQuery();
            new SQLiteCommand("CREATE TABLE Stats (Index INT NOT NULL,Amount VARCHAR NOT NULL,Stat INT NOT NULL,Card VARCHAR NOT NULL)", m_dbConnection).ExecuteNonQuery();
            */

            modurls.Add("Vanilla", "https://landfall.se/rounds");
            Unbound.Instance.ExecuteAfterFrames(100, delegate {
                camObj = MainMenuHandler.instance.gameObject.transform.parent.parent.GetComponentInChildren<MainCam>().gameObject;
                camera = camObj.GetComponent<Camera>();
                lighObj = camObj.transform.parent.Find("Lighting/LightCamera").gameObject;
                lightCam = lighObj.GetComponent<Camera>();
                Destroy(lighObj.GetComponent<SFRenderer>());
                MainMenuHandler.instance.gameObject.SetActive(false);
                Resolution resolution = new Resolution()
                {
                    height = renderHeight + 80,
                    width = renderWidth + 80,
                    refreshRate = 65
                };
                Optionshandler.instance.SetFullScreen(Optionshandler.FullScreenOption.Windowed);
                Optionshandler.instance.SetResolution(resolution);
                MainMenuHandler.instance.gameObject.SetActive(false);
                /*getModData();/**/
                StartCoroutine(GetCardData());
                DoThemes();/**/
                getMapData();
            });
        }
        public void getModData()
        {
            List<CardInfo> cards = ((List<CardInfo>)typeof(ModdingUtils.Utils.Cards).GetField("hiddenCards", BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(ModdingUtils.Utils.Cards.instance)).ToList();
            cards.AddRange(allCards);
            cards.ForEach(c => GetCardSorce(c));
            UnityEngine.Debug.Log("Writing");
            string ModData = "{\"Mods\":[";
            modurls.Keys.ToList().ForEach(mod => {
                ModData += $"{{\"Mod\":\"{mod}\",\"Url\":\"{modurls[mod]}\"}},";
            });
            ModData = ModData.Substring(0, ModData.Length - 1) + "]}";
            File.WriteAllText("./jsons/Mods.Json", ModData);
            UnityEngine.Debug.Log("done");
        }

        public void getMapData()
        {
            foreach(string map in LevelManager.levels.Keys)
            {
                MapsData += $"{{\"ID\":\"{LevelManager.GetVisualName(map)}\",\"Name\":\"{LevelManager.GetVisualName(LevelManager.levels[map].name)}\",\"Mod\":\"{LevelManager.levels[map].category}\"}},";
            }
            MapsData = MapsData.Substring(0, MapsData.Length - 1) + "]}";
            File.WriteAllText("./jsons/Maps.Json", MapsData);
        }

        public IEnumerator GetCardArt(CardInfo cardInfo, Guid g)
        {

            var camPosition = camObj.transform.position;
            GameObject cardObject = PhotonNetwork.PrefabPool.Instantiate(cardInfo.name, new Vector3(camPosition.x, camPosition.y - 2f, camPosition.z + 10), camera.transform.rotation);
            cardObject.SetActive(true);

            var backObject = FindObjectInChildren(cardObject, "Back");
            var backCanvas = backObject.AddComponent<Canvas>();
            backCanvas.enabled = false;

            foreach (CurveAnimation curveAnimation in cardObject.gameObject.GetComponentsInChildren<CurveAnimation>(true))
            {
                foreach (var curveAnimationAnimation in curveAnimation.animations)
                {
                    curveAnimationAnimation.speed = 1000;
                }
            }
            foreach (GeneralParticleSystem generalParticleSystem in cardObject.gameObject.GetComponentsInChildren<GeneralParticleSystem>())
            {
                //generalParticleSystem.enabled = false;
            }

            var scaleShake = cardObject.gameObject.GetComponentInChildren<ScaleShake>();
            scaleShake.enabled = false;
            var setScaleToZero = cardObject.gameObject.GetComponentInChildren<SetScaleToZero>();
            setScaleToZero.enabled = false;

            cardObject.gameObject.GetComponentInChildren<CardVisuals>().firstValueToSet = true;
            var canvas = cardObject.GetComponentInChildren<Canvas>();
            canvas.transform.localScale *= 2f;

            CardAnimationHandler cardAnimationHandler = null;
            if (cardInfo.cardArt != null)
            {
                var artObject = FindObjectInChildren(cardObject.gameObject, "Art");
                if (artObject != null)
                {
                    cardAnimationHandler = artObject.AddComponent<CardAnimationHandler>();
                    cardAnimationHandler.ToggleAnimation(false);
                }
            }

            // Wait for the card to appear presentable
            for (int _ = 0; _ < 5; _++) yield return null;

            var images = new List<byte[]>();
            if (cardAnimationHandler != null)
            {
                int frames = System.Math.Max((int)(renderFrames * cardAnimationHandler.langth), renderFrames);
                for (float i = 0; i < frames; i += 1)
                {
                    yield return TakeScreenshot(cardAnimationHandler, Mathf.InverseLerp(0, frames, i), images);
                    if(cardAnimationHandler.langth <= 0.3f) yield return new WaitForSecondsRealtime(0.1f);
                }
            }
            else
            {
                for (float i = 0; i < renderFrames; i += 1)
                {
                    yield return TakeScreenshot(cardAnimationHandler, Mathf.InverseLerp(0, renderFrames, i), images);
                    yield return new WaitForSecondsRealtime(0.1f);
                }
            }
            CreateGif(cardInfo, images, g);

            Destroy(cardObject);

            /*
            GameObject cardObject = PhotonNetwork.PrefabPool.Instantiate(card,
                new Vector3(camObj.transform.position.x, camObj.transform.position.y-2f, camObj.transform.position.z+10),
                camera.transform.rotation);
            cardObject.transform.localScale = Vector3.one * 2;
            cardObject.SetActive(true);
            cardObject.GetComponentInChildren<CardVisuals>().firstValueToSet = true;
            Destroy(FindObjectInChildren(cardObject, "UI_ParticleSystem"));
            for(int _ = 0; _< 60; _++) yield return null;
            const int resWidth = 350;
            const int resHeight = 500;
            var rt = new RenderTexture(resWidth, resHeight, 24);
            camera.targetTexture = rt;
            var screenShot = new Texture2D(resWidth, resHeight, TextureFormat.RGB24, false);
            camera.Render();
            RenderTexture.active = rt;
            screenShot.ReadPixels(new Rect(0, 0, resWidth, resHeight), 0, 0);


            camera.targetTexture = null;
            RenderTexture.active = null;
            // Destroy render texture to avoid null errors
            Destroy(rt);

            // Get camera to take picture from

            var rt1 = new RenderTexture(resWidth, resHeight, 24);
            lightCam.targetTexture = rt1;
            var screenShot1 = new Texture2D(resWidth, resHeight, TextureFormat.ARGB32, false);
            lightCam.Render();
            RenderTexture.active = rt1;
            screenShot1.ReadPixels(new Rect(0, 0, resWidth, resHeight), 0, 0);
            lightCam.targetTexture = null;
            RenderTexture.active = null;
            // Destroy render texture to avoid null errors
            Destroy(rt1);

            // Combine the two screenshots if alpha is zero on screenshot 1
            var pixels = screenShot.GetPixels(0, 0, screenShot.width, screenShot.height);
            var pixels1 = screenShot1.GetPixels(0, 0, screenShot.width, screenShot.height);
            for (var i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].a != 0)
                {
                    pixels[i].a = 1;
                }
                if (pixels1[i].a != 0)
                {
                    pixels1[i].a = 1;
                }

                if (pixels[i].a == 0)
                {
                    pixels[i] = pixels1[i];
                }
            }
            screenShot.SetPixels(pixels);

            var bytes = screenShot.EncodeToPNG();
            var dir = Directory.CreateDirectory(Path.Combine(Paths.ConfigPath, "Cards"));
            var filename = Path.Combine(dir.FullName, card + ".png");
            File.WriteAllBytes($"./cards/{g}.png", bytes);
            Destroy(cardObject);
            /*
            Destroy(camera);
            Destroy(cardObject);
            Destroy(screenShot);
            Destroy(rt);*/
        }

        private void CreateGif(CardInfo cardInfo, List<byte[]> images, Guid g)
        {
            new Thread(() =>
            {
                // Create empty image.
                var gif = SixLabors.ImageSharp.Image.Load(images[0], new PngDecoder());
                // gif = gif.Clone(x => x.Crop(100, 100));

                // Set animation loop repeat count to 5.
                var gifMetaData = gif.Metadata.GetGifMetadata();
                gifMetaData.RepeatCount = 0;

                // Set the delay until the next image is displayed.
                GifFrameMetadata metadata = gif.Frames.RootFrame.Metadata.GetGifMetadata();
                metadata.FrameDelay = 1;
                if (images.Count > 0)
                {
                    for (int i = 1; i < images.Count; i++)
                    {
                        // Create a color image, which will be added to the gif.
                        var image = SixLabors.ImageSharp.Image.Load(images[i], new PngDecoder());
                        // image = image.Clone(x => x.Crop(100, 100));

                        // Set the delay until the next image is displayed.
                        metadata = image.Frames.RootFrame.Metadata.GetGifMetadata();
                        metadata.FrameDelay = 1;

                        // Add the color image to the gif.
                        gif.Frames.AddFrame(image.Frames.RootFrame);
                    }
                }

                gif.SaveAsGif($"{Paths.GameRootPath}/cards/{g}.gif");
            }).Start();
        }

        private IEnumerator TakeScreenshot(CardAnimationHandler cardAnimationHandler, float time, ICollection<byte[]> images, bool transparent = false)
        {
            if (cardAnimationHandler != null)
            {
                if (time != 0)
                {
                    cardAnimationHandler.ToggleAnimation(true);
                    cardAnimationHandler.SetAnimationPoint(time);
                }
                else
                {
                    //yield break;
                }
            }
            else
            {
                if (time != 0)
                {
                    // We go back if the card has no art and it took the first screenshot
                    //yield break;
                }
            }

            // Wait for the card to change frame
            for (int _ = 0; _ < 4; _++) yield return null;

            var scrTexture = transparent ? new Texture2D(renderWidth, renderHeight, TextureFormat.ARGB32, false) : new Texture2D(renderWidth, renderHeight, TextureFormat.RGB24, false);
            RenderTexture scrRenderTexture = new RenderTexture(scrTexture.width, scrTexture.height, 24);
            RenderTexture camRenderTexture = camera.targetTexture;

            camera.targetTexture = scrRenderTexture;
            camera.Render();
            camera.targetTexture = camRenderTexture;

            RenderTexture.active = scrRenderTexture;
            scrTexture.ReadPixels(new Rect(0, 0, scrTexture.width, scrTexture.height), 0, 0);
            scrTexture.Apply();

            Texture2D srcLightTexture = new Texture2D(renderWidth, renderHeight, TextureFormat.RGB24, false);
            RenderTexture srcLightRenderTexture = new RenderTexture(srcLightTexture.width, srcLightTexture.height, 24);
            RenderTexture camLightRenderTexture = lightCam.targetTexture;

            lightCam.targetTexture = srcLightRenderTexture;
            lightCam.Render();
            lightCam.targetTexture = camLightRenderTexture;

            RenderTexture.active = srcLightRenderTexture;
            srcLightTexture.ReadPixels(new Rect(0, 0, srcLightTexture.width, srcLightTexture.height), 0, 0);
            srcLightTexture.Apply();

            // Combine the two screenshots if alpha is zero on screenshot 1
            var pixels = scrTexture.GetPixels(0, 0, scrTexture.width, scrTexture.height);
            var pixels1 = srcLightTexture.GetPixels(0, 0, srcLightTexture.width, srcLightTexture.height);
            for (var i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].a != 0)
                {
                    pixels[i].a = 1;
                }
                if (pixels1[i].a != 0)
                {
                    pixels1[i].a = 1;
                }
                if (pixels[i].a == 0)
                {
                    pixels[i] = pixels1[i];
                }
            }
            scrTexture.SetPixels(pixels);

            var bytes = scrTexture.EncodeToPNG();
            images.Add(bytes);
        }


        public void DoThemes()
        {
            CardChoice.instance.cardThemes.ToList().ForEach(theme => {
                ThemeData += $"{{\"Name\":\"{theme.themeType}\",\"Color\":\"{FormatColor(theme.targetColor)}\"}},";
            });
            ThemeData = ThemeData.Substring(0, ThemeData.Length - 1);
            ThemeData += "]}";
            File.WriteAllText("./jsons/Themes.Json", ThemeData);
        }
        public string FormatColor(UnityEngine.Color color)
        {
            string r = $"00{((int)(color.r * 256)):x}";
            string g = $"00{((int)(color.g * 256)):x}";
            string b = $"00{((int)(color.b * 256)):x}";
            return r.Substring(r.Length - 2) + g.Substring(g.Length - 2) + b.Substring(b.Length - 2);
        }
        public string GetCardSorce(CardInfo card)
        {
            try
            {
                PluginInfo[] pluginInfos = BepInEx.Bootstrap.Chainloader.PluginInfos.Values.ToArray();
                foreach (PluginInfo info in pluginInfos)
                {
                    bool isSorce = false;
                    Assembly mod = Assembly.LoadFile(info.Location);
                    if (card.gameObject.GetComponent<CustomCard>() != null)
                    {
                        if (card.gameObject.GetComponent<CustomCard>().GetType().Assembly.GetName().ToString() == mod.GetName().ToString())
                        {
                            isSorce = true;
                        }
                    }
                    else 
                    {
                        BundleLocations.Keys.Where(v => BundleLocations[v].FullName == mod.FullName).ToList().ForEach(bundle => {
                            bundle.LoadAllAssets<GameObject>().ToList().ForEach(asset =>
                            {
                                if(asset.GetComponent<CardInfo>() != null && asset.GetComponent<CustomCard>() == null)
                                {
                                    if(asset.GetComponent<CardInfo>().cardName == card.cardName)
                                    {
                                        isSorce = true;
                                    }
                                }
                            });
                        });
                    }
                    if (isSorce)
                    {
                        string local = info.Location;
                        while (local.Last() != '\\')
                        {
                            local = local.Substring(0, local.Length - 1);
                        }
                        File.WriteAllBytes($"./modicons/{info.Metadata.Name}.png", File.ReadAllBytes(local + "icon.png"));
                        if (!modurls.ContainsKey(info.Metadata.Name))
                        {
                            local = local.Substring(0, local.Length - 1);
                            string author = "";
                            string package = "";
                            while (local.Last() != '-')
                            {
                                package = local.Last().ToString() + package;
                                local = local.Substring(0, local.Length - 1);
                            }
                            local = local.Substring(0, local.Length - 1);
                            while (local.Last() != '\\')
                            {
                                author = local.Last().ToString() + author;
                                local = local.Substring(0, local.Length - 1);
                            }
                            modurls.Add(info.Metadata.Name, $"{BaseURL}{author}/{package}");
                            UnityEngine.Debug.Log($"{BaseURL}{author}/{package}");
                        }
                        return info.Metadata.Name;
                    }
                }
            }
            catch(Exception e) { UnityEngine.Debug.Log(e); }
            return "Vanilla";
        }

        public string GetCardClass(CardInfo card)
        {
            CustomCard card1 = card.GetComponent<CustomCard>();
            try
            {
                if (card1 != null)
                    card1.Callback();
            }
            catch { }
            ClassNameMono mono = card.gameObject.GetComponent<ClassNameMono>();
            if (mono == null)
                return "None";
            else
                return mono.className;
        }

        public IEnumerator GetCardData()
        {
            List<CardInfo> hiddenCards = ((List<CardInfo>)typeof(ModdingUtils.Utils.Cards).GetField("hiddenCards", BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(ModdingUtils.Utils.Cards.instance)).ToList();
            int count = allCards.Count + hiddenCards.Count;
            int curnet = 0;
            foreach (CardInfo card in allCards)
            {
                UnityEngine.Debug.Log($"{++curnet}/{count}");
                if (!ProsessedCards.Contains(card.name))
                {
                    Guid g = Guid.NewGuid();
                    yield return GetCardArt(card, g);
                    UnityEngine.Debug.Log(GetCardSorce(card));
                    //new SQLiteCommand($"INSERT INTO Cards (ID,Name,Rarity,Theme,Descripion,IsCurse) VALUES ({card.name},{card.cardName},{card.colorTheme},{card.cardDestription},{card.categories.Contains(CurseManager.instance.curseCategory)})", m_dbConnection).ExecuteNonQuery();
                    CardData += $"{{\"ID\":\"{card.name}\",\"art\":\"{g}\",\"Name\":\"{card.cardName}\",\"Rarity\":\"{card.rarity}\",\"Theme\":\"{card.colorTheme}\",\"Description\":\"{(card.cardDestription == null ? "" : card.cardDestription.Replace("\"", "\\\""))}\",\"IsCurse\":{card.categories.Contains(CurseManager.instance.curseCategory).ToString().ToLower()},\"Mod\":\"{GetCardSorce(card)}\",\"Multiple\":{card.allowMultiple.ToString().ToLower()},\"Class\":\"{GetCardClass(card)}\",\"Hidden\":{false.ToString().ToLower()}}},";
                    UnityEngine.Debug.Log($"({card.name}):\n{card.cardName}({card.rarity},Theme:{card.colorTheme})\n{card.cardDestription}\n{GetCardStats(card)}\n\n\n");
                    ProsessedCards.Add(card.name);
                }
                else { UnityEngine.Debug.Log("Error Duplict Card"); }
            }
            foreach (CardInfo card in hiddenCards)
            {
                UnityEngine.Debug.Log($"{++curnet}/{count}");
                if (!ProsessedCards.Contains(card.name))
                {
                    Guid g = Guid.NewGuid();
                    yield return GetCardArt(card, g);
                    UnityEngine.Debug.Log(GetCardSorce(card));
                    //new SQLiteCommand($"INSERT INTO Cards (ID,Name,Rarity,Theme,Descripion,IsCurse) VALUES ({card.name},{card.cardName},{card.colorTheme},{card.cardDestription},{card.categories.Contains(CurseManager.instance.curseCategory)})", m_dbConnection).ExecuteNonQuery();
                    CardData += $"{{\"ID\":\"{card.name}\",\"art\":\"{g}\",\"Name\":\"{card.cardName}\",\"Rarity\":\"{card.rarity}\",\"Theme\":\"{card.colorTheme}\",\"Description\":\"{(card.cardDestription == null ? "" : card.cardDestription.Replace("\"", "\\\""))}\",\"IsCurse\":{card.categories.Contains(CurseManager.instance.curseCategory).ToString().ToLower()},\"Mod\":\"{GetCardSorce(card)}\",\"Multiple\":{card.allowMultiple.ToString().ToLower()},\"Class\":\"{GetCardClass(card)}\",\"Hidden\":{true.ToString().ToLower()}}},";
                    UnityEngine.Debug.Log($"({card.name}):\n{card.cardName}({card.rarity},Theme:{card.colorTheme})\n{card.cardDestription}\n{GetCardStats(card)}\n\n\n");
                    ProsessedCards.Add(card.name);
                }
                else { UnityEngine.Debug.Log("Error Duplict Card"); }
            }
            CardData = CardData.Substring(0, CardData.Length - 1);
            CardData += "]}";
            CardData = CardData.Replace("\n", "\\n");
            StatsData = StatsData.Substring(0, StatsData.Length - 1);
            StatsData += "]}";
            StatsData = StatsData.Replace("\n", "\\n");
            File.WriteAllText("./jsons/Cards.Json", CardData);
            File.WriteAllText("./jsons/Stats.Json", StatsData);
            string ModData = "{\"Mods\":[";
            modurls.Keys.ToList().ForEach(mod => {
                ModData += $"{{\"Mod\":\"{mod}\",\"Url\":\"{modurls[mod]}\"}},";
            });
            ModData = ModData.Substring(0,ModData.Length - 1) + "]}";
            File.WriteAllText("./jsons/Mods.Json", ModData);
        }
        public string GetCardStats(CardInfo card)
        {
            CardInfoStat[] cardStats = card.cardStats;
            string value = "";

            for (int i = 0; i < cardStats.Length; i++) 
            {
                CardInfoStat stat = cardStats[i];
                // new SQLiteCommand($"INSERT INTO Cards (Index,Amount,Stat,Card) VALUES ({i},{stat.amount},{stat.stat}{card.name})", m_dbConnection).ExecuteNonQuery();
                StatsData += $"{{\"Idex\":{i},\"Amount\":\"{(stat.amount == null ? "" : stat.amount.Replace("\"", "\\\""))}\",\"Stat\":\"{(stat.stat == null ? "" : stat.stat.Replace("\"", "\\\""))}\",\"Card\":\"{card.name}\"}},";
                value += $"({(stat.positive?"Pos":"Neg")}){stat.amount} {stat.stat}\n";
            }

            return value;
        }

        private static GameObject FindObjectInChildren(GameObject gameObject, string gameObjectName)
        {
            Transform[] children = gameObject.GetComponentsInChildren<Transform>(true);
            return (from item in children where item.name == gameObjectName select item.gameObject).FirstOrDefault();
        }

        [HarmonyPatch(typeof(Jotunn.Utils.AssetUtils), "LoadAssetBundleFromResources")]
        [HarmonyPostfix]
        public static void bundles(Assembly resourceAssembly, ref AssetBundle __result)
        {
            BundleLocations[__result] = resourceAssembly;
        }

        internal class CardAnimationHandler : MonoBehaviour
        {
            private bool toggled;
            private float _langth = -1;
            public float langth { get { if (_langth == -1) _langth = Function(); return _langth; } }
            private Dictionary<Animator, float> langths = new Dictionary<Animator, float>();

            public void ToggleAnimation(bool value)
            {
                foreach (Animator animatorComponent in gameObject.GetComponentsInChildren<Animator>())
                {
                    if (animatorComponent.enabled == value) continue;
                    animatorComponent.enabled = value;
                }

                foreach (PositionNoise positionComponent in gameObject.GetComponentsInChildren<PositionNoise>())
                {
                    if (positionComponent.enabled == value) continue;
                    positionComponent.enabled = value;
                }

                toggled = value;
            }

            public void SetAnimationPoint(float time)
            {
                foreach (Animator animatorComponent in gameObject.GetComponentsInChildren<Animator>())
                {
                    animatorComponent.speed = 0;
                    if (animatorComponent.layerCount > 0)
                    {
                        if (!langths.ContainsKey(animatorComponent))
                            langths[animatorComponent] = (float)Math.Round(animatorComponent.GetCurrentAnimatorStateInfo(0).length,1);
                        
                        float time2 = langth / langths[animatorComponent];
                        time2 *= time;
                        time2 = time2 - UnityEngine.Mathf.Floor(time2);
                        animatorComponent.Play(animatorComponent.GetCurrentAnimatorStateInfo(0).fullPathHash, 0, time2);
                    }
                }
            }

            private float Function()
            {
                List<float> times = new List<float>();
                times.Add(0.25f);
                foreach (Animator animatorComponent in gameObject.GetComponentsInChildren<Animator>())
                {
                    if (animatorComponent.layerCount > 0)
                    {
                        times.Add((float)Math.Round(animatorComponent.GetCurrentAnimatorStateInfo(0).length, 1));
                    }
                }
                return UnityEngine.Mathf.Max(times.ToArray());
            }

            static float gcd(float n1, float n2)
            {
                if (n2 == 0)
                {
                    return n1;
                }
                else
                {
                    return gcd(n2, n1 % n2);
                }
            }

            private void Update()
            {
                ToggleAnimation(toggled);
            }
        }
    }
}
