using UnityEngine;
using UnityEngine.UI;

// A passive inner-shadow overlay. InputField remains the only focus/input owner.
public sealed class CatLifeInputFocus : MonoBehaviour
{
    [SerializeField] private InputField input;
    [SerializeField] private Image rings;
    public void Configure(InputField field, Image surface) { input = field; rings = surface; }
    private void LateUpdate() { rings.enabled = input.isFocused; }
    private void OnDisable() { rings.enabled = false; }
}
