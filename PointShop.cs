using Life;
using Life.Network;
using Life.UI;
using ModKit.Helper;
using ModKit.Interfaces;
using Newtonsoft.Json;
using PointShop.Classes;
using PointShop.Entities;
using PointShop.Points;
using System.IO;
using _menu = AAMenu.Menu;
using mk = ModKit.Helper.TextFormattingHelper;

namespace PointShop
{
    public class PointShop : ModKit.ModKit
    {
        public static string ConfigDirectoryPath;
        public static string ConfigPointShopPath;
        public static PointShopConfig _pointShopConfig;

        public PointShop(IGameAPI api) : base(api)
        {
            PluginInformations = new PluginInformations(AssemblyHelper.GetName(), "1.0.0", "Aarnow");
        }

        public override void OnPluginInit()
        {
            base.OnPluginInit();
            InitConfig();
            _pointShopConfig = LoadConfigFile(ConfigPointShopPath);

            Orm.RegisterTable<PointShop_Logs>();
            Orm.RegisterTable<PointShop_Item>();

            Orm.RegisterTable<Shop>();
            PointHelper.AddPattern("Shop", new Shop(false));
            AAMenu.AAMenu.menu.AddBuilder(PluginInformations, "Shop", new Shop(false), this);

            InsertMenu();

            ModKit.Internal.Logger.LogSuccess($"{PluginInformations.SourceName} v{PluginInformations.Version}", "initialisé");
        }

        #region Config
        private void InitConfig()
        {
            try
            {
                ConfigDirectoryPath = DirectoryPath + "/PointShop";
                ConfigPointShopPath = Path.Combine(ConfigDirectoryPath, "pointShopConfig.json");

                if (!Directory.Exists(ConfigDirectoryPath)) Directory.CreateDirectory(ConfigDirectoryPath);
                if (!File.Exists(ConfigPointShopPath)) InitPointShopConfig();
            }
            catch (IOException ex)
            {
                ModKit.Internal.Logger.LogError("InitDirectory", ex.Message);
            }
        }

        private void InitPointShopConfig()
        {
            PointShopConfig pointShopConfig = new PointShopConfig();
            string json = JsonConvert.SerializeObject(pointShopConfig);
            File.WriteAllText(ConfigPointShopPath, json);
        }

        private PointShopConfig LoadConfigFile(string path)
        {
            if (File.Exists(path))
            {
                string jsonContent = File.ReadAllText(path);
                PointShopConfig pointShopConfig = JsonConvert.DeserializeObject<PointShopConfig>(jsonContent);

                return pointShopConfig;
            }
            else return null;
        }
        #endregion

        public void InsertMenu()
        {
            _menu.AddAdminPluginTabLine(PluginInformations, 5, "PointShop", (ui) =>
            {
                Player player = PanelHelper.ReturnPlayerFromPanel(ui);
                PointShopPanel(player);
            });
        }

        public void PointShopPanel(Player player)
        {
            //Déclaration
            Panel panel = PanelHelper.Create("PointShop", UIPanel.PanelType.TabPrice, player, () => PointShopPanel(player));

            //Corps
            panel.AddTabLine($"{mk.Color("Appliquer la configuration", mk.Colors.Info)}", _ =>
            {
                _pointShopConfig = LoadConfigFile(ConfigPointShopPath);
                panel.Refresh();
            });

            panel.NextButton("Sélectionner", () => panel.SelectTab());
            panel.AddButton("Retour", _ => AAMenu.AAMenu.menu.AdminPluginPanel(player));
            panel.CloseButton();

            //Affichage
            panel.Display();
        }
    }
}
