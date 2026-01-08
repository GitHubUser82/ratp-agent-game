using System.Collections;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Wave")]
    public TravelerAI travelerPrefab;
    public Transform[] spawnPoints;
    public int totalTravelersInWave = 20;
    public float spawnInterval = 0.6f;

    [Header("Rules")]
    public int maxFraudSuccess = 3;

    [Header("Turnstiles")]
    public Turnstile[] turnstiles;

    [Header("Exits")]
    public Transform[] exitPoints;

    [Header("UI")]
    public GameUI gameUI;

    int _spawned;
    int _finished;
    int _fraudSuccess;
    bool _ended;

    void Awake()
    {
        if (Instance != null && Instance != this)
            Debug.LogError("MULTIPLE GameManagers in scene!");

        Instance = this;
    }

    void Start()
    {
        RefreshUI();
        StartCoroutine(SpawnWave());
    }

    IEnumerator SpawnWave()
    {
        while (_spawned < totalTravelersInWave)
        {
            SpawnOne();
            _spawned++;
            yield return new WaitForSeconds(spawnInterval);
        }
    }

    void SpawnOne()
    {
        var sp = spawnPoints[Random.Range(0, spawnPoints.Length)];
        var traveler = Instantiate(travelerPrefab, sp.position, sp.rotation);

        var chosenTurnstile = turnstiles[Random.Range(0, turnstiles.Length)];
        traveler.Initialize(this, chosenTurnstile);
    }

    // Called by TravelerAI
    public void ReportTravelerFinished(bool fraudSucceeded)
    {
        if (_ended) return;

        _finished++;

        if (fraudSucceeded)
            _fraudSuccess++;

        RefreshUI();

        if (_fraudSuccess > maxFraudSuccess)
        {
            EndGame(false);
            return;
        }

        if (_finished >= totalTravelersInWave)
        {
            EndGame(true);
        }
    }

    void EndGame(bool win)
    {
        _ended = true;

        if (gameUI)
            gameUI.ShowEndPanel(win);

        Time.timeScale = 0f;
    }

    void RefreshUI()
    {
        if (!gameUI) return;

        gameUI.SetWaveProgress(_finished, totalTravelersInWave);
        gameUI.SetFraud(_fraudSuccess, maxFraudSuccess);
    }

    public Transform GetRandomExit()
    {
        return exitPoints[Random.Range(0, exitPoints.Length)];
    }
}
