using Heimevernet_Prosjekt.Models.Entities;

namespace Heimevernet_Prosjekt.DataAccess;

// Resource types that admin can add to, rename and delete (same pattern as IResourceRepository)
public interface IResourceTypeRepository
{
    IReadOnlyCollection<ResourceType> GetAll();
    ResourceType? GetById(int id);
    bool Exists(string name);
    ResourceType Create(string name, string? examples);
    bool Update(int id, string name, string? examples);
    int CountResourcesUsing(string name);
    bool Delete(int id);
}