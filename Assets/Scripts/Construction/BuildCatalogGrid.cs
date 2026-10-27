using UnityEngine;
using UnityEngine.UI;

namespace JuegoCriminal.Construction
{
    // Only the generated catalogue is automatic; the HUD and its controls are
    // ordinary RectTransforms owned by the prefab's author.
    [ExecuteAlways,DisallowMultipleComponent,RequireComponent(typeof(GridLayoutGroup))]
    public sealed class BuildCatalogGrid : MonoBehaviour
    {
        [Min(30)] public float rowHeight=80;
        void OnEnable()=>Refresh();
        void OnRectTransformDimensionsChange()=>Refresh();
        void LateUpdate()=>Refresh();
        public void Refresh()
        {
            var grid=GetComponent<GridLayoutGroup>();if(!grid)return;
            var rect=(RectTransform)transform;
            float width=Mathf.Max(1,(rect.rect.width-grid.padding.horizontal-grid.spacing.x)/2);
            var size=new Vector2(width,rowHeight);
            if(grid.constraint!=GridLayoutGroup.Constraint.FixedColumnCount)grid.constraint=GridLayoutGroup.Constraint.FixedColumnCount;
            if(grid.constraintCount!=2)grid.constraintCount=2;
            if(grid.cellSize!=size)grid.cellSize=size;
        }
    }
}
