using UnityEngine;

// Tablo prefab'ının kök objesine ekleyin.
// Inspector'dan resim atayabilir veya koddan SetImage(...) çağırabilirsiniz.
[RequireComponent(typeof(Renderer))]
public class Tablo : MonoBehaviour
{
    [SerializeField] private Texture2D resim;
    [SerializeField] private string tuvalMateryalAdi = "Tuval";

    private Material tuvalMateryali;

    private void Awake()
    {
        var renderer = GetComponent<Renderer>();
        // renderer.materials, her tablo için materyalin kendi kopyasını verir;
        // böylece bir tablonun resmini değiştirmek diğerlerini etkilemez.
        foreach (var m in renderer.materials)
        {
            if (m.name.Contains(tuvalMateryalAdi))
            {
                tuvalMateryali = m;
                break;
            }
        }

        if (tuvalMateryali == null)
            Debug.LogWarning($"'{tuvalMateryalAdi}' materyali bulunamadı: {name}", this);

        if (resim != null) SetImage(resim);
    }

    public void SetImage(Texture2D yeniResim)
    {
        if (tuvalMateryali == null) return;
        tuvalMateryali.mainTexture = yeniResim; // Built-in ve URP Lit ile çalışır
    }
}
