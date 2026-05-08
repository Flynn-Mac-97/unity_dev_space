using UnityEngine;

public class NpcInteraction : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private GameObject menuRoot;
    [SerializeField] private NpcDialogueData dialogueData;
    [SerializeField] private float interactRange = 2.5f;

    private bool _menuVisible;

    private void Update()
    {
        // Hide the radial menu while dialogue is open
        if (Time.timeScale == 0f)
        {
            if (_menuVisible)
            {
                _menuVisible = false;
                menuRoot.SetActive(false);
            }
            return;
        }

        if (player == null || menuRoot == null) return;

        bool inRange = Vector3.Distance(transform.position, player.position) <= interactRange;
        if (inRange == _menuVisible) return;

        _menuVisible = inRange;
        menuRoot.SetActive(_menuVisible);
    }

    public void OnTalk()
    {
        if (DialogueManager.Instance != null && dialogueData != null)
            DialogueManager.Instance.Open(dialogueData);
    }

    public void OnTrade()  => Debug.Log("[NPC] Trade");
    public void OnAttack() => Debug.Log("[NPC] Attack");
}
