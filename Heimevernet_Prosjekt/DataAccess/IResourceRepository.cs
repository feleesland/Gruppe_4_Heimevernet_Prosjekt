using Heimevernet_Prosjekt.Models.Entities;
using Heimevernet_Prosjekt.Models.ViewModels.Resource;


namespace Heimevernet_Prosjekt.DataAccess
{
    public interface IResourceRepository
    {
        Resource Create(ResourceViewModel model);
        bool Delete(int id);
        IReadOnlyCollection<Resource> GetAll();
        Resource? GetById(int id);
        bool Update(int id, ResourceViewModel model);
        int RemoveOwner(string ownerId);
        int DeleteAllByOwner(string ownerId);
        bool SetStatus(int id, string status);
        bool SetUnavailable(int id, string? reason, DateTime? untilUtc);

    }
}
