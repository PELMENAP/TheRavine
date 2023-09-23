using System;
using UnityEngine;

namespace Modern2D
{

	[System.Serializable]
	public struct StylizedShadowCaster : IComparable<StylizedShadowCaster>
	{
		public Vector2 pivotOffset;
		//transform of the objects than casts the shadow
		public Transform shadowCaster;
		//transform of shadow renderer
		public Transform shadow;
		//rotate in order to change light direction, pivot of shadow renderer
		public Transform shadowPivot;
		//sprite renderer of the shadow
		public SpriteRenderer shadowSr;
		//sprite renderer of the caster
		public SpriteRenderer shadowCasterSr;
		// sahdow pivot
		public PivotSourceMode pivotMode;
		// custom pivot object 
        public Transform pivotObject;
		// x-orientation
		public bool flipX;
		// custom shadow layer
		public bool customShadowLayer;
		// shadow layer
		public string shadowLayer;

		public StylizedShadowCaster2D casterComponent;

        public StylizedShadowCaster(Transform shadowCaster, Transform shadow, SpriteRenderer shadowSr, Transform pivot, Vector2 pivotOffset, PivotSourceMode mode, Transform pivotObj = null, StylizedShadowCaster2D caster = null, bool flipX = false, bool cst = false, string shadowLayer = "Shadows")
		{
			this.shadowCaster = shadowCaster;
			this.shadow = shadow;
			this.shadowSr = shadowSr;
			this.shadowCasterSr = shadowCaster.GetComponent<SpriteRenderer>();
			this.shadowPivot = pivot;
			this.pivotOffset = pivotOffset;
			this.pivotMode = mode;
			this.pivotObject = pivotObj;
			this.flipX = flipX;
			this.customShadowLayer = cst;
			this.shadowLayer = shadowLayer;
			this.casterComponent = caster;

        }

		public int CompareTo(StylizedShadowCaster other)
		{
			if (shadowCaster == other.shadowCaster) return 0;
			else return -1;
		}
	}

}