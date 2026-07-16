using UnityEngine;
using UnityEngine.UI;

public class UIPanelController : MonoBehaviour

{
    [Header("UI")]
    [SerializeField] private GameObject panel;
    [SerializeField] private Image pdfImage;

    private Sprite[] currentPages;
    private int currentPage;

    public void Open(Sprite[] pages)
    {
        if (pages == null || pages.Length == 0)
            return;

        currentPages = pages;
        currentPage = 0;

        pdfImage.sprite = currentPages[currentPage];
        panel.SetActive(true);
    }

    public void Close()
    {
        panel.SetActive(false);
    }

    public void NextPage()
    {
        if (currentPages == null)
            return;

        if (currentPage < currentPages.Length - 1)
        {
            currentPage++;
            pdfImage.sprite = currentPages[currentPage];
        }
    }

    public void PrevPage()
    {
        if (currentPages == null)
            return;

        if (currentPage > 0)
        {
            currentPage--;
            pdfImage.sprite = currentPages[currentPage];
        }
    }
}
