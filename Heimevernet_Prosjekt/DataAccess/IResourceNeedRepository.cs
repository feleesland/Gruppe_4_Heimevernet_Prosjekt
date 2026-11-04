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

    }
}
