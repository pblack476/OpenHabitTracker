using OpenHabitTracker.App;
using OpenHabitTracker.Data;
using OpenHabitTracker.Data.Entities;
using OpenHabitTracker.Data.Models;

namespace OpenHabitTracker.Services;

/// <summary>
/// Saves the detached checklist of a newly created task/recurring task only.
/// Existing items are never updated or deleted by duplication.
/// </summary>
internal static class CopiedItemPersistence
{
    public static async Task SaveNewItems(ClientState clientState, ItemsModel parent)
    {
        if (parent.Items is not { Count: > 0 })
            return;

        // Create the whole checklist first. New IDs and owner bookkeeping are
        // established only after the database assigned each one.
        foreach (ItemModel item in parent.Items)
        {
            ItemEntity copy = new()
            {
                ParentId = parent.Id,
                Title = item.Title,
                DoneAt = null
            };

            await clientState.DataAccess.AddItem(copy);

            item.Id = copy.Id;
            item.ParentId = parent.Id;
            item.DoneAt = null;

            clientState.Items ??= new();
            clientState.Items[item.Id] = item;
        }
    }
}
