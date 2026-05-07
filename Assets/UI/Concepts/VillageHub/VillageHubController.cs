using UnityEngine;
using UnityEngine.UIElements;

public class VillageHubController : MonoBehaviour
{
    // Task data
    private class TaskData
    {
        public string id;
        public string title;
        public string benefit;
        public string[] npcs;
        public (string name, int need, int have)[] requirements;
        public bool affordable;
    }

    private TaskData[] tasks;
    private TaskData selectedTask;
    private VisualElement selectedCard;

    // UI elements
    private VisualElement taskDetail;
    private Label detailTitle, detailBenefit, tooltipText;
    private VisualElement detailNPCs, detailRequirements;
    private Button confirmButton;

    // Resources
    private int scrap = 18, wood = 12, seeds = 15, cores = 7, filters = 5, glass = 4, battery = 2;

    private void Awake()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;

        taskDetail = root.Q<VisualElement>("TaskDetail");
        detailTitle = root.Q<Label>("DetailTitle");
        detailBenefit = root.Q<Label>("DetailBenefit");
        detailNPCs = root.Q<VisualElement>("DetailNPCs");
        detailRequirements = root.Q<VisualElement>("DetailRequirements");
        tooltipText = root.Q<Label>("TooltipText");
        confirmButton = root.Q<Button>("ConfirmButton");
        var backButton = root.Q<Button>("BackButton");

        confirmButton.clicked += OnConfirm;
        backButton.clicked += () => Debug.Log("[VillageHub] Back to village");

        BuildTaskData();
        WireCards(root);
        UpdateResourceDisplay(root);
    }

    private void BuildTaskData()
    {
        tasks = new TaskData[]
        {
            new TaskData
            {
                id = "water",
                title = "Repair Water Channel",
                benefit = "Restores irrigation and unlocks new garden plots",
                npcs = new[] { "Botanist", "Builder" },
                requirements = new[] { ("Scrap", 12, scrap), ("Water Filters", 4, filters) }
            },
            new TaskData
            {
                id = "greenhouse",
                title = "Rebuild Greenhouse",
                benefit = "Increases food production and improves village mood",
                npcs = new[] { "Cook", "Botanist", "Mochi" },
                requirements = new[] { ("Wood", 8, wood), ("Seeds", 10, seeds), ("Glass Panels", 3, glass) }
            },
            new TaskData
            {
                id = "robot",
                title = "Wake Helper Robot",
                benefit = "Adds an automated helper who assists with repairs",
                npcs = new[] { "Engineer", "Builder" },
                requirements = new[] { ("Machine Cores", 5, cores), ("Scrap", 6, scrap), ("Solar Battery", 1, battery) }
            }
        };

        foreach (var t in tasks)
        {
            t.affordable = true;
            foreach (var (_, need, have) in t.requirements)
                if (have < need) { t.affordable = false; break; }
        }
    }

    private void WireCards(VisualElement root)
    {
        var cardMap = new (string id, string elementName)[]
        {
            ("water", "CardWater"),
            ("greenhouse", "CardGreenhouse"),
            ("robot", "CardRobot")
        };

        foreach (var (id, elName) in cardMap)
        {
            var card = root.Q<VisualElement>(elName);
            var task = System.Array.Find(tasks, t => t.id == id);

            if (!task.affordable)
                card.AddToClassList("task-card-disabled");

            card.RegisterCallback<ClickEvent>(evt =>
            {
                if (!task.affordable)
                {
                    tooltipText.text = "Not enough resources for this project";
                    return;
                }
                SelectTask(task, card);
            });
        }
    }

    private void SelectTask(TaskData task, VisualElement card)
    {
        // Deselect previous
        if (selectedCard != null)
            selectedCard.RemoveFromClassList("task-card-selected");

        selectedTask = task;
        selectedCard = card;
        card.AddToClassList("task-card-selected");

        // Show detail panel
        taskDetail.RemoveFromClassList("task-detail-hidden");
        detailTitle.text = task.title;
        detailBenefit.text = task.benefit;

        // NPCs
        detailNPCs.Clear();
        foreach (var npc in task.npcs)
        {
            var badge = new Label(npc);
            badge.AddToClassList("npc-badge");
            badge.AddToClassList(GetNPCClass(npc));
            detailNPCs.Add(badge);
        }

        // Requirements
        detailRequirements.Clear();
        foreach (var (name, need, have) in task.requirements)
        {
            var row = new VisualElement();
            row.AddToClassList("requirement-row");

            var icon = new Label("◆");
            icon.AddToClassList("req-icon");
            icon.AddToClassList(GetReqIconClass(name));

            var reqName = new Label(name);
            reqName.AddToClassList("req-name");

            var count = new Label($"{need} / {have}");
            count.AddToClassList("req-count");
            count.AddToClassList(have >= need ? "req-met" : "req-unmet");

            row.Add(icon);
            row.Add(reqName);
            row.Add(count);
            detailRequirements.Add(row);
        }

        confirmButton.SetEnabled(task.affordable);
        tooltipText.text = task.affordable
            ? "Ready to begin — confirm when you're set"
            : "Gather more resources before starting";
    }

    private void OnConfirm()
    {
        if (selectedTask == null) return;
        Debug.Log($"[VillageHub] Confirmed: {selectedTask.title}");
        tooltipText.text = $"Started: {selectedTask.title}";
    }

    private void UpdateResourceDisplay(VisualElement root)
    {
        SetCount(root, "ScrapCount", scrap);
        SetCount(root, "WoodCount", wood);
        SetCount(root, "SeedsCount", seeds);
        SetCount(root, "CoresCount", cores);
        SetCount(root, "FiltersCount", filters);
        SetCount(root, "GlassCount", glass);
        SetCount(root, "BatteryCount", battery);
    }

    private void SetCount(VisualElement root, string name, int value)
    {
        var el = root.Q<Label>(name);
        if (el != null) el.text = value.ToString();
    }

    private static string GetNPCClass(string npc) => npc switch
    {
        "Botanist" => "npc-botanist",
        "Builder" => "npc-builder",
        "Cook" => "npc-cook",
        "Mochi" => "npc-child",
        "Engineer" => "npc-engineer",
        _ => ""
    };

    private static string GetReqIconClass(string name) => name switch
    {
        "Scrap" => "req-icon-scrap",
        "Wood" => "req-icon-wood",
        "Seeds" => "req-icon-seed",
        "Machine Cores" => "req-icon-core",
        "Water Filters" => "req-icon-filter",
        "Glass Panels" => "req-icon-glass",
        "Solar Battery" => "req-icon-battery",
        _ => ""
    };
}
