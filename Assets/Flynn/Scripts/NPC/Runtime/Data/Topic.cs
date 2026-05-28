using UnityEngine;

[CreateAssetMenu(menuName = "Dialogue/Topic", fileName = "Topic_New")]
public class Topic : ScriptableObject
{
    public enum Category { Place, Person, Event, Item, Rumor, Lore, Other }

    [Tooltip("Stable key used in save data and runtime flags. Lower case, underscores. Example: cave_north")]
    public string topicId = "topic_new";

    public string displayName = "New Topic";

    public Category category = Category.Other;

    [TextArea(2, 5)]
    public string description = "";

    public string GetSafeId()
    {
        return string.IsNullOrWhiteSpace(topicId) ? name : topicId.Trim();
    }

    public string GetSafeDisplayName()
    {
        return string.IsNullOrWhiteSpace(displayName) ? name : displayName.Trim();
    }
}
