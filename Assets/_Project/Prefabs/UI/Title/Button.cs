using UnityEngine;

public class PdfButton : MonoBehaviour
{
    [SerializeField] private UIPanelController viewer;
    [SerializeField] private Sprite[] pages;

    public void OpenPdf()
    {
        viewer.Open(pages);
    }
}