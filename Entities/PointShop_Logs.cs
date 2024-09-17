using SQLite;

namespace PointShop.Entities
{
    internal class PointShop_Logs : ModKit.ORM.ModEntity<PointShop_Logs>
    {
        [AutoIncrement][PrimaryKey] public int Id { get; set; }
        public int ShopId { get; set; }
        public int CharacterId { get; set; }
        public string CharacterFullName { get; set; }
        public int ItemId { get; set; }
        public int BizId { get; set; }
        public int Quantity { get; set; }
        public bool IsPurchase { get; set; }
        public double Price { get; set; }
        public int CreatedAt { get; set; }

        public PointShop_Logs()
        {
        }
    }
}
