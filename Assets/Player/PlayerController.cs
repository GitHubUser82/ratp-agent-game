using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class PlayerController : MonoBehaviour
{
    [Header("Contrôle")]
    public float interactionRange = 1.6f;   // Distance nécessaire pour contrôler
    public float controlDuration = 1.2f;    // Durée du contrôle

    [Header("Raycast")]
    public LayerMask clickMask;              // Couche du sol cliquable

    private NavMeshAgent _agent;
    private TravelerAI _targetTraveler;
    private bool _isControlling;
    private bool _isChasing;

    private Camera _mainCamera;

    void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _mainCamera = Camera.main;
    }

    void Update()
    {
        if (_isControlling) return;

        // Gestion du clic droit
        if (Input.GetMouseButtonDown(1))
        {
            HandleRightClick();
        }

        // Poursuite active d’un voyageur ciblé
        if (_isChasing && _targetTraveler)
        {
            // Si la cible n’est plus contrôlable, le trajet est annulé
            if (_targetTraveler.IsBusy)
            {
                _targetTraveler = null;
                _isChasing = false;
                return;
            }

            // Suit en continu la position du voyageur
            _agent.SetDestination(_targetTraveler.transform.position);

            float distance = Vector3.Distance(
                transform.position,
                _targetTraveler.transform.position
            );

            // Déclenche le contrôle quand on est vraiment à portée
            if (!_agent.pathPending &&
                distance <= interactionRange &&
                _agent.remainingDistance <= interactionRange + 0.1f)
            {
                StartCoroutine(ControlTravelerRoutine(_targetTraveler));
                _targetTraveler = null;
                _isChasing = false;
            }
        }
    }

    void HandleRightClick()
    {
        Ray ray = _mainCamera.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, 200f))
            return;

        // Clic sur un voyageur : poursuite
        TravelerAI traveler = hit.collider.GetComponentInParent<TravelerAI>();
        if (traveler && !traveler.IsBusy)
        {
            _targetTraveler = traveler;
            _isChasing = true;
            return;
        }

        // Clic sur le sol : déplacement libre
        if (((1 << hit.collider.gameObject.layer) & clickMask) != 0)
        {
            _targetTraveler = null;
            _isChasing = false;
            _agent.SetDestination(hit.point);
        }
    }

    IEnumerator ControlTravelerRoutine(TravelerAI traveler)
    {
        if (!traveler) yield break;

        _isControlling = true;

        // Immobilise le joueur
        _agent.isStopped = true;

        // Lance le contrôle côté voyageur
        yield return traveler.GetControlled(controlDuration);

        // Reprise du mouvement
        _agent.isStopped = false;
        _isControlling = false;
    }
}
