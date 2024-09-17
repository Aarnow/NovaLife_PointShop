using Life;
using ModKit.Helper;
using ModKit.Interfaces;
using PointShop.Entities;
using PointShop.Points;

namespace PointShop
{
    public class PointShop : ModKit.ModKit
    {
        public PointShop(IGameAPI api) : base(api)
        {
            PluginInformations = new PluginInformations(AssemblyHelper.GetName(), "1.0.0", "Aarnow");
        }

        public override void OnPluginInit()
        {
            base.OnPluginInit();

            Orm.RegisterTable<PointShop_Logs>();
            Orm.RegisterTable<PointShop_Item>();

            Orm.RegisterTable<Shop>();
            PointHelper.AddPattern("Shop", new Shop(false));
            AAMenu.AAMenu.menu.AddBuilder(PluginInformations, "Shop", new Shop(false), this);

            ModKit.Internal.Logger.LogSuccess($"{PluginInformations.SourceName} v{PluginInformations.Version}", "initialisé");
        }
    }
}
