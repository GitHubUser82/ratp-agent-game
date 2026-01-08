using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Turnstile : MonoBehaviour
{
    public enum TurnstileType { Normal, Gate }
    public TurnstileType type = TurnstileType.Normal;

    [Header("Queue")]
    public Transform queuePointsRoot;
    public Transform exitPoint;

    public float badgeTime = 0.5f;
    public float jumpTime = 0.35f;

    private readonly Queue<TravelerAI> _queue = new Queue<TravelerAI>();
    private bool _busy;

    public IReadOnlyList<Transform> QueuePoints => _queuePoints;
    private List<Transform> _queuePoints = new List<Transform>();

    [Header("Turnstile animation")]
    public Transform rotatingPart;
    public float rotationAngle = 60f;
    public float rotationDuration = 0.15f;

    private Vector3 _localRotationAxis;
    private Coroutine _rotationRoutine;

    void Awake()
    {
        _localRotationAxis = Vector3.forward;

        _queuePoints.Clear();
        if (queuePointsRoot)
        {
            foreach (Transform child in queuePointsRoot)
                _queuePoints.Add(child);
        }
    }

    public bool CanFraud => type == TurnstileType.Normal;

    public void UpgradeToGate()
    {
        type = TurnstileType.Gate;
        Debug.Log($"{name} amélioré en PORTE (pas de saut).");
    }

    public void PlayTurnstileAnimation()
    {
        if (!rotatingPart) return;

        if (_rotationRoutine != null)
            StopCoroutine(_rotationRoutine);

        _rotationRoutine = StartCoroutine(RotateTurnstileRoutine());
    }

    IEnumerator RotateTurnstileRoutine()
    {
        Quaternion start = rotatingPart.localRotation;
        Quaternion end = start * Quaternion.AngleAxis(rotationAngle, _localRotationAxis);

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.001f, rotationDuration);
            rotatingPart.localRotation = Quaternion.Slerp(start, end, t);
            yield return null;
        }

        _rotationRoutine = null;
    }
    
    public void FinishedServingFront()
    {
        if (_queue.Count > 0)
            _queue.Dequeue();

        NotifyQueueAdvanced();
        _busy = false;
        TryServeNext();
    }
    
    public void Enqueue(TravelerAI traveler)
    {
        if (!_queue.Contains(traveler))
            _queue.Enqueue(traveler);

        traveler.SetQueueIndex(_queue.Count - 1);
        TryServeNext();
    }
    
    public void TryServeNextExternal()
    {
        TryServeNext();
    }

    private void TryServeNext()
    {
        if (_busy) return;
        if (_queue.Count == 0) return;

        var next = _queue.Peek();
        if (!next) { _queue.Dequeue(); TryServeNext(); return; }

        if (!next.IsAtFrontOfQueue()) return;

        _busy = true;
        next.BeginPassingThrough(this);
    }

    private void NotifyQueueAdvanced()
    {
        int i = 0;
        foreach (var t in _queue)
        {
            t.SetQueueIndex(i);
            i++;
        }
    }

}