using OpenHabitTracker.Data.Models;

namespace OpenHabitTracker.Services;

public interface IItemService
{
    ItemModel? SelectedItem { get; set; }
    ItemModel? NewItem { get; set; }
    Task Initialize(ItemsModel? items);
    Task AddItem(ItemsModel? items);
    Task<int> AddItemsFromLines(ItemsModel? items, string text);
    Task UpdateItem(string title);
    Task SetIsDone(ItemModel item, bool done);
    Task DeleteItem(ItemsModel? items, ItemModel item);
}
