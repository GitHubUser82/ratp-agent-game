using TMPro;
using UnityEngine;

public class GameUI : MonoBehaviour
{
    public TMP_Text waveText;
    public TMP_Text fraudText;

    [Header("End")]
    public GameObject endPanel;
    public TMP_Text endText;

    void Awake()
    {
        if (endPanel)
            endPanel.SetActive(false);
    }

    public void SetWaveProgress(int finished, int total)
    {
        if (waveText) waveText.text = $"Voyageurs passés : {finished}/{total}";
    }

    public void SetFraud(int success, int max)
    {
        if (fraudText) fraudText.text = $"Fraudes réussies : {success}/{max}";
    }

    public void ShowEndPanel(bool win)
    {
        if (endPanel) endPanel.SetActive(true);
        if (endText) endText.text = win ? "VICTOIRE !" : "DÉFAITE ! Trop de fraudes.";
    }
}