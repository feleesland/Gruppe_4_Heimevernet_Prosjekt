using Heimevernet_Prosjekt.Models.Entities;
using Heimevernet_Prosjekt.Models.ViewModels.ResourceNeed;

namespace Heimevernet_Prosjekt.DataAccess
{
    public interface IResourceNeedRepository
    {
        //ResourceNeed Create(ResourceNeedViewModel model); ikke i bruk
        bool Delete(int id);
        IReadOnlyCollection<ResourceNeed> GetAll();
        ResourceNeed? GetById(int id);
        //bool Update(int id, ResourceNeedViewModel model); ikke i bruk
        int RemoveOwner(string ownerId);
        int DeleteAllByOwner(string ownerId);

        // "Meld behov": a new need with its lines, saved together
        ResourceNeed CreateWithItems(ReportNeedViewModel model, string ownerId);

        // "Legg til i ...": add lines to an active need. False if it doesn't exist or is closed
        bool AddItems(int needId, IEnumerable<NeedLineInput> lines);

        // The need with its lines (for the need page)
        ResourceNeed? GetWithItems(int id);

        // Active needs whose circle contains the point, nearest first (with their lines)
        IReadOnlyCollection<ResourceNeed> GetActiveAt(double latitude, double longitude);
        bool Close(int id);
        bool UpdateWithItems(int id, ReportNeedViewModel model);
        void AddLog(int needId, string userId, string userName, string text);
        IReadOnlyList<ResourceNeedLog> GetLog(int needId);

        // "Ofte brukt" in "Meld behov": the types that are asked for in the most needs, most first
        IReadOnlyList<string> GetPopularTypes(int count);

        // "Info til innbyggere": public updates on a need, newest first
        void AddUpdate(int needId, string userId, string userName, string text);
        IReadOnlyList<NeedUpdate> GetUpdates(int needId);
    }
}
