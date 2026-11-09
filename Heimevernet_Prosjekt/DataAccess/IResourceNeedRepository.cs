using Heimevernet_Prosjekt.Models.Entities;
using Heimevernet_Prosjekt.Models.ViewModels.ResourceNeed;

namespace Heimevernet_Prosjekt.DataAccess
{
    public interface IResourceNeedRepository
    {
        ResourceNeed Create(ResourceNeedViewModel model);
        bool Delete(int id);
        IReadOnlyCollection<ResourceNeed> GetAll();
        ResourceNeed? GetById(int id);
        bool Update(int id, ResourceNeedViewModel model);
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
    }
}
