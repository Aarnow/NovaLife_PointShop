using SQLite;

namespace PointShop.Entities
{
    public class PointShop_Item : ModKit.ORM.ModEntity<PointShop_Item>
    {
        [AutoIncrement][PrimaryKey] public int Id { get; set; }
        public int ItemId { get; set; }
        public double Price { get; set; }
        public bool IsBuyable { get; set; }
        public bool IsResellable { get; set; }

        public PointShop_Item()
        {
        }
    }
}
