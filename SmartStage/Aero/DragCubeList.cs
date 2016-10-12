using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SmartStage
{
	public class SmartDragCubeList
	{
		public SmartDragCubeList(Part part, List<Part> availableParts)
		{
			part.DragCubes.SetDragWeights();
			None = part.DragCubes.None;
			this.part = part;
			if (!part.DragCubes.None) {
				weightedDepth = GetPrivate<IEnumerable<float>>("weightedDepth", part.DragCubes).ToArray();
				weightedArea = GetPrivate<IEnumerable<float>>("weightedArea", part.DragCubes).ToArray();
				weightedDragOrig = GetPrivate<IEnumerable<float>>("weightedDragOrig", part.DragCubes).ToArray();

				areaOccluded = GetPrivate<IEnumerable<float>>("weightedArea", part.DragCubes).ToArray();
				weightedDrag = GetPrivate<IEnumerable<float>>("weightedDragOrig", part.DragCubes).ToArray();
				for (int i = part.attachNodes.Count - 1; i >= 0; i--)
				{
					AttachNode attachNode = part.attachNodes [i];
					if (attachNode != null)
					{
						Part attachedPart = attachNode.attachedPart;
						if (attachedPart != null && availableParts.Contains(attachedPart))
						{
							if (attachedPart.dragModel == Part.DragModel.CUBE)
							{
								Vector3 normalized = attachNode.orientation.normalized;
								Vector3 a = Quaternion.Inverse(attachedPart.partTransform.rotation) * part.partTransform.rotation * normalized;
								float facingAreaSum = GetFacingAreaSum(-a, GetPrivate<float[]>("weightedArea", attachedPart.DragCubes));
								AreaToCubeOperation(normalized, facingAreaSum, (float fArea, float cArea) => Mathf.Max (0, cArea - fArea));
								attachNode.contactArea = facingAreaSum;
							}
						}
					}
				}
				if (part.srfAttachNode != null) {
					if (part.srfAttachNode.attachedPart != null && availableParts.Contains(part.srfAttachNode.attachedPart)) {
						AttachNode attachNode = part.srfAttachNode;
						Vector3 normalized2 = attachNode.orientation.normalized;
						float facingAreaSum2 = GetFacingAreaSum (normalized2, GetPrivate<float[]>("weightedArea", part.DragCubes));
						attachNode.contactArea = facingAreaSum2;
					}
				}
				return;
			}
		}

		public static T GetPrivate<T>(String field, object obj)
		{
			var f = obj.GetType().GetField(field, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
			return (T) f.GetValue(obj);
		}

		static float GetFacingAreaSum(Vector3 direction, float[] faceAreaArray)
		{
			float num = 0;
			for (int i = 0; i < 6; i++) {
				float num2 = Mathf.Clamp01 (Vector3.Dot (direction, DragCubeList.GetFaceDirection ((DragCube.DragFace)i)));
				num += faceAreaArray [i] * num2;
			}
			return num;
		}

		void AreaToCubeOperation (Vector3 direction, float area, Func<float, float, float> operation)
		{
			for (int i = 0; i < 6; i++) {
				float num = Mathf.Clamp01 (Vector3.Dot (direction, DragCubeList.GetFaceDirection ((DragCube.DragFace)i)));
				if (num > 0) {
					this.areaOccluded[i] = operation (area * num, this.areaOccluded[i]);
					float num2 = this.weightedArea [i] - this.areaOccluded[i];
					if (this.areaOccluded[i] > 0) {
						this.weightedDrag[i] = Mathf.Max(0, (this.weightedDragOrig[i] * this.weightedArea[i] - num2) / this.areaOccluded[i]);
					}
					else {
						this.weightedDrag[i] = 1E-05f;
					}
				}
			}
		}

		public readonly bool None;

		float[] areaOccluded;
		float[] weightedDrag;
		float[] weightedDepth;
		float[] weightedArea;
		float[] weightedDragOrig;
		Part part;

		public DragCubeList.CubeData AddSurfaceDragDirection(Vector3 direction, float machNumber)
		{
			part.DragCubes.SetDrag(direction, machNumber);
			var liftCurves = GetPrivate<PhysicsGlobals.LiftingSurfaceCurve>("liftCurves", part.DragCubes);
			float num = 0;
			DragCubeList.CubeData result = default(DragCubeList.CubeData);
			result.dragVector = direction;
			for (int i = 0; i < 6; i++) {
				Vector3 faceDirection = DragCubeList.GetFaceDirection((DragCube.DragFace)i);
				float num2 = Vector3.Dot (direction, faceDirection);
				float dotNormalized = (num2 + 1) * 0.5f;
				float num3 = PhysicsGlobals.DragCurveValue(PhysicsGlobals.SurfaceCurves, dotNormalized, machNumber);
				float num4 = this.areaOccluded [i] * num3;
				float num5 = this.weightedDrag [i];
				float num6 = num5;
				if (num6 < 1) {
					num6 = PhysicsGlobals.DragCurveCd.Evaluate (num6);
				}
				result.area += num4;
				result.areaDrag += num4 * num6;
				result.crossSectionalArea += this.areaOccluded [i] * Mathf.Clamp01 (num2);
				if (num5 < 0.01) {
					num5 = 1;
				}
				if (num5 < 1) {
					num5 = 1 / num5;
				}
				result.exposedArea += num4 / PhysicsGlobals.DragCurveMultiplier.Evaluate (machNumber) * num5;
				if (num2 > 0) {
					num += num2;
					double num7 = (double)liftCurves.liftCurve.Evaluate (num2);
					if (!double.IsNaN (num7)) {
						result.liftForce += -faceDirection * (num2 * this.areaOccluded [i] * this.weightedDrag [i] * (float)num7);
					}
					result.depth += num2 * this.weightedDepth [i];
					result.dragCoeff += num2 * num6;
				}
			}
			float num8 = 1 / num;
			result.depth *= num8;
			result.dragCoeff *= num8;
			return result;
		}
	}
}

