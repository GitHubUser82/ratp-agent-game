using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class TravelerAI : MonoBehaviour
{
    public enum State
    {
        GoingToQueue,
        Queuing,
        Passing,
        PostGate,   //état du voyageur après avoir passé un tourniquet
        Controlled,
        Exited
    }
    [Header("Fraud")]
    [Range(0f, 1f)] public float fraudProbability = 0.25f;

    [Header("Movement")]
    public float arriveDistance = 0.25f;
    public float queueSnapSpeed = 12f;
    
    [Header("Jump (fraud)")]
    public float jumpHeight = 1.1f;
    public float jumpDuration = 0.35f;
    
    [Header("Visual control bar")]
    public GameObject controlBarRoot;
    public RectTransform controlBarBackground;
    public RectTransform controlBarFill;

    private float _fillMaxWidth;
    
    private NavMeshAgent _agent;
    private GameManager _gm;
    private Turnstile _turnstile;

    private State _state;
    private int _queueIndex = -1;
    private bool _isFraud;          // a choisi de frauder
    private bool _fraudSucceeded;   // est passé sans se faire attraper
    private bool _passing;
    public bool IsBusy => _state != State.PostGate; // Le voyageur est cliquable uniquement dans l'état PostGate
    
    void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        
        if (controlBarRoot)
        {
            controlBarRoot.SetActive(false); //La barre de progression d'un contrôle est cachée par défaut 
            _fillMaxWidth = controlBarBackground.rect.width;
        }

    }

    public void Initialize(GameManager gameManager, Turnstile t)
    {
        _gm = gameManager;
        _turnstile = t;

        _state = State.GoingToQueue;

        // Choisir l’intention au spawn
        _isFraud = Random.value < fraudProbability && _turnstile.CanFraud;

        // Rejoindre la file immédiatement
        _turnstile.Enqueue(this);
        GoToMyQueuePoint();
    }

    public void SetQueueIndex(int index)
    {
        _queueIndex = index;
        if (_state == State.Queuing || _state == State.GoingToQueue)
            GoToMyQueuePoint();
    }

    void GoToMyQueuePoint()
    {
        if (!_turnstile) return;
        if (_queueIndex < 0) return;

        var points = _turnstile.QueuePoints;
        if (points == null || points.Count == 0) return;

        int clamped = Mathf.Clamp(_queueIndex, 0, points.Count - 1);
        Vector3 target = points[clamped].position;

        _agent.isStopped = false;
        _agent.SetDestination(target);

        _state = State.Queuing;
    }

    public bool IsAtFrontOfQueue()
    {
        if (!_turnstile) return false;
        if (_queueIndex != 0) return false;

        var points = _turnstile.QueuePoints;
        if (points == null || points.Count == 0) return false;

        Vector3 front = points[0].position;

        // Ignorer les différences de hauteur
        Vector2 a = new Vector2(transform.position.x, transform.position.z);
        Vector2 b = new Vector2(front.x, front.z);

        // Exiger aussi que l’agent se considère comme pratiquement arrivé
        bool closeInXZ = Vector2.Distance(a, b) <= 0.4f;
        bool arrived = !_agent.pathPending && (_agent.remainingDistance <= Mathf.Max(arriveDistance, 0.2f));

        return closeInXZ && arrived;
    }

    private float _serveCheckCooldown;
    void Update()
    {
        // Gestion de la file d’attente
        if (_state == State.Queuing)
        {
            SnapToQueuePointIfClose();
        }

        // Déclenchement du passage au tourniquet
        if (_state == State.Queuing && _queueIndex == 0 && !_passing)
        {
            _serveCheckCooldown -= Time.deltaTime;
            if (_serveCheckCooldown <= 0f && IsAtFrontOfQueue())
            {
                _serveCheckCooldown = 0.25f;
                _turnstile.TryServeNextExternal();
            }
        }

        // Vérifie si le voyageur a atteint une sortie après le tourniquet
        if (_state == State.PostGate && !_agent.pathPending)
        {
            if (_agent.remainingDistance <= Mathf.Max(arriveDistance, 0.3f))
            {
                _state = State.Exited;
                
                _gm.ReportTravelerFinished(_fraudSucceeded); // Fraude ajoutée au compteur
                
                Destroy(gameObject);
            }
        }
    }


    void SnapToQueuePointIfClose()
    {
        var points = _turnstile.QueuePoints;
        if (points == null || points.Count == 0) return;

        int clamped = Mathf.Clamp(_queueIndex, 0, points.Count - 1);
        Vector3 p = points[clamped].position;

        if (Vector3.Distance(transform.position, p) <= 0.6f && !_agent.pathPending)
        {
            // Accrochage progressif pour garder une file visuellement propre
            transform.position = Vector3.Lerp(transform.position, p, Time.deltaTime * queueSnapSpeed);
        }
    }

    public void BeginPassingThrough(Turnstile t)
    {
        if (_passing) return;
        _passing = true;
        _state = State.Passing;

        StartCoroutine(PassRoutine(t));
    }

    IEnumerator PassRoutine(Turnstile t)
    {
        // Détermine si la fraude est toujours possible
        bool willFraud = _isFraud && t.CanFraud;

        if (willFraud)
        {
            // Saut au-dessus du tourniquet (arc simple)
            Vector3 startPos = transform.position;
            Vector3 endPos = t.exitPoint.position;

            // Désactive l'agent pour éviter toute correction NavMesh
            _agent.isStopped = true;
            _agent.enabled = false;

            float time = 0f;
            while (time < jumpDuration)
            {
                time += Time.deltaTime;
                float u = Mathf.Clamp01(time / Mathf.Max(0.001f, jumpDuration));

                // Interpolation horizontale
                Vector3 pos = Vector3.Lerp(startPos, endPos, u);

                // Arc vertical (sinus)
                float yOffset = Mathf.Sin(u * Mathf.PI) * jumpHeight;
                pos.y += yOffset;

                transform.position = pos;
                yield return null;
            }

            transform.position = endPos;
            _agent.enabled = true;
            _agent.isStopped = false;

            // Fraude réussie si pas interceptée
            _fraudSucceeded = true;
        }
        else
        {
            // Passage normal : animation du tourniquet
            t.PlayTurnstileAnimation();

            _agent.isStopped = false;
            _agent.SetDestination(t.exitPoint.position);

            yield return new WaitForSeconds(t.badgeTime);
        }

        // Dirige ensuite vers une sortie finale
        Transform exit = GameManager.Instance.GetRandomExit();
        _agent.SetDestination(exit.position);

        // Libère le tourniquet après un court délai
        yield return new WaitForSeconds(0.25f);

        _state = State.PostGate;
        _passing = false;

        t.FinishedServingFront();
    }


    // Appelé par le joueur lorsqu’il est suffisamment proche
    public IEnumerator GetControlled(float controlDuration)
    {
        if (_state == State.Exited) yield break;

        // Mémorise l’état précédent
        State previousState = _state;
        _state = State.Controlled;

        // Gèle le déplacement si possible
        if (_agent.enabled)
            _agent.isStopped = true;

        // Affiche la barre de contrôle
        if (controlBarRoot)
        {
            controlBarRoot.SetActive(true);
            _fillMaxWidth = controlBarBackground.rect.width;
        }

        // Reset visuel du fill
        if (controlBarFill)
            controlBarFill.sizeDelta = new Vector2(0f, controlBarFill.sizeDelta.y);

        float t = 0f;
        while (t < controlDuration)
        {
            t += Time.deltaTime;
            float progress = Mathf.Clamp01(t / controlDuration);

            // Mise à jour de la largeur du fill
            if (controlBarFill)
            {
                float width = Mathf.Lerp(0f, _fillMaxWidth, progress);
                controlBarFill.sizeDelta =
                    new Vector2(width, controlBarFill.sizeDelta.y);
            }

            yield return null;
        }

        // Cache la barre
        if (controlBarRoot)
            controlBarRoot.SetActive(false);

        // Annule la fraude si nécessaire
        if (_fraudSucceeded)
            _fraudSucceeded = false;

        // Restaure l’état précédent
        _state = previousState;

        if (_agent.enabled)
        {
            _agent.isStopped = false;

            if (_state == State.Queuing)
            {
                GoToMyQueuePoint();
            }
            // State.Passing : ne rien faire
            // State.PostGate : continue vers la sortie
        }
    }

    
}
