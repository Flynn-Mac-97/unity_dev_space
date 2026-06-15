using UnityEngine;
using Flynn.Events;

namespace Flynn.Tutorial
{
    /// <summary>
    /// Starts the <see cref="TutorialDirector"/> the first time the intro NPC's dialogue
    /// closes — i.e. after the player finishes the transmitter NPC's opening chat. Pure
    /// bus subscriber; put it on the same object as the director (set the director's
    /// auto-start OFF so the conversation drives the start).
    /// </summary>
    public class NpcDialogueTutorialTrigger : MonoBehaviour
    {
        [SerializeField] private TutorialDirector _director;
        [Tooltip("npcId (NpcAuthoringLink) of the intro NPC whose first chat starts the tutorial.")]
        [SerializeField] private string _introNpcId;

        private bool _started;

        private void Awake()
        {
            if (_director == null) _director = GetComponent<TutorialDirector>();
        }

        private void OnEnable()
        {
            if (GameEventBus.Instance != null)
                GameEventBus.Instance.Subscribe<NpcDialogueClosed>(OnDialogueClosed);
        }

        private void OnDisable()
        {
            if (GameEventBus.Instance != null)
                GameEventBus.Instance.Unsubscribe<NpcDialogueClosed>(OnDialogueClosed);
        }

        private void OnDialogueClosed(NpcDialogueClosed e)
        {
            if (_started || e.NpcId != _introNpcId) return;
            _started = true;
            if (_director != null) _director.StartTutorial();
        }
    }
}
