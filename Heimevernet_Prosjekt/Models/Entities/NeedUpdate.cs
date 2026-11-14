namespace Heimevernet_Prosjekt.Models.Entities
{
    // "Info til innbyggere": short public updates on a need, shown on the front page and the map for everyone (help with AI)
    // Like ResourceNeedLog, but public. The name is saved as text, so it stays if the user is deleted
    public class NeedUpdate
    {
        public int Id { get; set; }
        public int ResourceNeedId { get; set; }
        public string Text { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public DateTime At { get; set; } = DateTime.UtcNow;
    }
}