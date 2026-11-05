using Heimevernet_Prosjekt.Models.Entities;
using Heimevernet_Prosjekt.Models.ViewModels.ResourceNeed;



namespace Heimevernet_Prosjekt.DataAccess
{
    public class ResourceNeedRepository : IResourceNeedRepository

    {
        // Not used anymore - EfResourceNeedRepository is used
        public bool Close(int id) => throw new NotSupportedException();
        private readonly Dictionary<int, ResourceNeed> _resourceNeeds = new();
        private int _nextId = 1;

        public ResourceNeedRepository()
        {
            //Add a few dummy resource needs for testing
            Create(new ResourceNeedViewModel { Name = "Resource Need 1", Description = "Description 1", Type = "Type A" });
            Create(new ResourceNeedViewModel { Name = "Resource Need 2", Description = "Description 2", Type = "Type B" });
            Create(new ResourceNeedViewModel { Name = "Resource Need 3", Description = "Description 3", Type = "Type C" });
        }

        public ResourceNeed Create(ResourceNeedViewModel model)
        {
            ArgumentNullException.ThrowIfNull(model);

            var resourceNeed = new ResourceNeed
            {
                Id = _nextId++,
                Name = model.Name,
                Description = model.Description,
                Type = model.Type
            };

            _resourceNeeds.Add(resourceNeed.Id, resourceNeed);
            return resourceNeed;
        }

        public ResourceNeed? GetById(int id)
        {
            return _resourceNeeds.GetValueOrDefault(id);
        }

        public IReadOnlyCollection<ResourceNeed> GetAll()
        {
            return _resourceNeeds.Values.ToList().AsReadOnly();
        }

        public bool Update(int id, ResourceNeedViewModel model)
        {
            ArgumentNullException.ThrowIfNull(model);

            if (!_resourceNeeds.TryGetValue(id, out var resourceNeed))
            {
                return false;
            }

            resourceNeed.Name = model.Name;
            resourceNeed.Description = model.Description;
            resourceNeed.Type = model.Type;
            return true;
        }

        public bool Delete(int id)
        {
            return _resourceNeeds.Remove(id);
        }

        public int RemoveOwner(string ownerId)
        {
            var resources = _resourceNeeds.Values.Where(r => r.OwnerId == ownerId).ToList();
            foreach (var resource in resources)
            {
                resource.OwnerId = string.Empty;
                resource.OwnerRemoved = true;
            }
            return resources.Count;
        }

        public int DeleteAllByOwner(string ownerId)
        {
            var resources = _resourceNeeds.Values.Where(r => r.OwnerId == ownerId).ToList();
            foreach (var resource in resources)
            {
                resource.IsDeleted = true;
                resource.DeletedAt = DateTime.UtcNow;
            }
            return resources.Count;
        }

        // The in-memory version is not used by the app (Program.cs uses EfResourceNeedRepository).
        // These only exist so the class still matches the interface - Docs: NotSupportedException
        // https://learn.microsoft.com/dotnet/api/system.notsupportedexception
        public ResourceNeed CreateWithItems(ReportNeedViewModel model, string ownerId)
            => throw new NotSupportedException("Bruk EfResourceNeedRepository.");

        public bool AddItems(int needId, IEnumerable<NeedLineInput> lines)
            => throw new NotSupportedException("Bruk EfResourceNeedRepository.");

        public ResourceNeed? GetWithItems(int id)
            => throw new NotSupportedException("Bruk EfResourceNeedRepository.");

        public IReadOnlyCollection<ResourceNeed> GetActiveAt(double latitude, double longitude)
            => throw new NotSupportedException("Bruk EfResourceNeedRepository.");


    }

}
