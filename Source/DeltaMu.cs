/*
This file is part of DeltaMu.

DeltaMu is free software: you can redistribute it and/or
modify it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

DeltaMu is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with DeltaMu.  If not, see
<http://www.gnu.org/licenses/>.
*/
using System;
using System.Collections;
using UnityEngine;

namespace DeltaMu {

	public class DeltaMu : PartModule
	{
		[KSPField]
		public string ShapeKeyTransform;

		public override void OnLoad (ConfigNode node)
		{
			if (HighLogic.LoadedScene == GameScenes.LOADING) {
				if (String.IsNullOrEmpty (ShapeKeyTransform)) {
					Debug.Log($"[DeltaMu] no shape key transform specified");
					return;
				}

				var skTrans = part.FindModelTransform (ShapeKeyTransform);
				if (skTrans == null) {
					Debug.Log($"[DeltaMu] could not find {ShapeKeyTransform}");
					return;
				}
				BuildShapeKeys (skTrans);
				return;
			}
		}

		Mesh BuildShapeKeys (Mesh inputMesh)
		{
			var mesh = new Mesh();
			var verts = inputMesh.vertices;
			var norms = inputMesh.normals;
			var tangs = inputMesh.tangents;
			var uvs = inputMesh.uv;
			var colors = inputMesh.colors32;
			var triangles = inputMesh.triangles;
			int maxVert = -1;
			foreach (int v in triangles) {
				if (v > maxVert) {
					maxVert = v;
				}
			}
			int numVerts = verts.Length;
			int baseVerts = maxVert + 1;
			int numKeys = numVerts / baseVerts;
			var newVerts = new Vector3[baseVerts];
			var newNorms = new Vector3[baseVerts];
			var newTangs = new Vector4[baseVerts];
			var newUVs = new Vector2[baseVerts];

			for (int i = 0; i < baseVerts; i++) {
				newVerts[i] = verts[i];
				newNorms[i] = norms[i];
				newTangs[i] = tangs[i];
				newUVs[i] = uvs[i];
			}
			mesh.UploadMeshData (false);
			mesh.vertices = newVerts;
			mesh.normals = newNorms;
			mesh.tangents = newTangs;
			mesh.uv = newUVs;
			mesh.triangles = triangles;
			var deltaT = new Vector3[baseVerts];
			for (int i = 1; i < numKeys; i++) {
				Debug.Log($"[DeltaMu] key: {i}");
				int base_index = i * baseVerts;
				for (int j = 0; j < baseVerts; j++) {
					newVerts[j] = verts[j + base_index];
					newNorms[j] = norms[j + base_index];
					Vector4 t = tangs[j + base_index];
					deltaT[j] = new Vector3(t.x, t.y, t.z);
					Debug.Log($"[DeltaMu] vertr[{j}]: {newVerts[j]}");
				}
				mesh.AddBlendShapeFrame(i.ToString(), 1, newVerts, newNorms, deltaT);
			}
			mesh.RecalculateBounds();
			mesh.UploadMeshData (false);
			return mesh;
		}

		void BuildShapeKeys (Transform skTrans)
		{
			GameObject go = skTrans.gameObject;
			var smr = go.GetComponent<SkinnedMeshRenderer> ();
			if (!smr) {
				var mf = go.GetComponent<MeshFilter> ();
				var r = go.GetComponent<MeshRenderer> ();
				if (!mf || !r) {
					Debug.Log ("[DeltaMu] missing shared mesh or renderer");
					Debug.Log ($"[DeltaMu] '{mf}' '{r}'");
					return;
				}
				smr = go.AddComponent<SkinnedMeshRenderer> ();
				smr.sharedMesh = BuildShapeKeys (mf.sharedMesh);
				smr.sharedMaterial = r.sharedMaterial;
				r.sharedMaterial = null;
				Destroy (mf);
				Destroy (r);
			} else {
				smr.sharedMesh = BuildShapeKeys (smr.sharedMesh);
			}
			Debug.Log ($"[DeltaMu] {smr.sharedMesh.blendShapeCount}");
		}

		float startTime;

		void Start ()
		{
			startTime = Time.unscaledTime;
		}

		void Update ()
		{
			float time = (Time.unscaledTime - startTime);
			var skTrans = part.FindModelTransform (ShapeKeyTransform);
			GameObject go = skTrans.gameObject;
			var smr = go.GetComponent<SkinnedMeshRenderer> ();
			int count = smr.sharedMesh.blendShapeCount;
			for (int i = 0; i < count; i++) {
				float n = 2 * i + 1;
				float d = 2 * Mathf.PI;
				float w = 0.5f - Mathf.Cos (5 * time * n / d) * 0.5f;
				smr.SetBlendShapeWeight(i, w);
			}
		}
	}
}
