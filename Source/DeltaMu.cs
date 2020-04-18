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
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace DeltaMu {

	public class DeltaMu : PartModule
	{
		[KSPField]
		public string ShapeKeyTransform;

		public struct ShapeKey {
			public string name;
			public float weight;
		}

		public class BlendShape {
			public string name;
			[UI_FloatRange (minValue = 0, maxValue = 1, stepIncrement = 0.05f)]
			[KSPField(guiActive=true, guiActiveEditor=true)]
			public float weight;
			public SkinnedMeshRenderer shapeMesh;
			public int shapeIndex;

			public BlendShape(string name, int index, SkinnedMeshRenderer shapeMesh)
			{
				this.name = name;
				this.weight = 1;
				this.shapeIndex = index;
				this.shapeMesh = shapeMesh;
			}
			public void ModifyValue (BaseField bf, object field)
			{
				shapeMesh.SetBlendShapeWeight(shapeIndex, weight);
			}
			public BaseField GetField()
			{
				var fields = new BaseFieldList (this);
				BaseField bf= fields[0];
				bf.guiName = name;
				UI_Control control = null;
				if (HighLogic.LoadedSceneIsEditor) {
					control = bf.uiControlEditor;
				} else if (HighLogic.LoadedSceneIsFlight) {
					control = bf.uiControlFlight;
				}
				if (control != null) {
					control.onFieldChanged += ModifyValue;
				}
				return bf;
			}
		}

		Dictionary<string, BlendShape> blendShapeDict;
		List<BlendShape> blendShapeList;

		public float this[int index]
		{
			get { return blendShapeList[index].weight; }
			set { blendShapeList[index].weight = value; }
		}

		public float this[string name]
		{
			get { return blendShapeDict[name].weight; }
			set { blendShapeDict[name].weight = value; }
		}

		new void Awake ()
		{
			base.Awake ();
			if (HighLogic.LoadedSceneIsGame) {
				blendShapeDict = new Dictionary<string, BlendShape> ();
				blendShapeList = new List<BlendShape> ();
			}
		}

		[SerializeField]
		string keyNodeString;

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
				ConfigNode keyNode = node.GetNode ("ShapeKeys");
				BuildShapeKeys (skTrans, keyNode);
				if (keyNode != null) {
					keyNodeString = keyNode.ToString ();
				}
				return;
			}
		}

		Mesh BuildShapeKeys (Mesh inputMesh, ShapeKey []keys)
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
			int numKeys = numVerts / baseVerts - 1;

			if (numKeys != keys.Length) {
				Debug.Log($"[DeltaMu] {part.name}:{ShapeKeyTransform}: keys mismatch: data:{numKeys} vs cfg:{keys.Length}");
				if (numKeys == keys.Length - 1) {
					Debug.Log("Make sure you did not include the basis shape");
				}
			}
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
			for (int i = 0; i < numKeys; i++) {
				int base_index = (i + 1) * baseVerts;
				for (int j = 0; j < baseVerts; j++) {
					newVerts[j] = verts[j + base_index];
					newNorms[j] = norms[j + base_index];
					Vector4 t = tangs[j + base_index];
					deltaT[j] = new Vector3(t.x, t.y, t.z);
				}
				if (i < keys.Length) {
					mesh.AddBlendShapeFrame(keys[i].name, keys[i].weight,
											newVerts, newNorms, deltaT);
				} else {
					mesh.AddBlendShapeFrame(i.ToString(), 1,
											newVerts, newNorms, deltaT);
				}
			}
			mesh.RecalculateBounds();
			mesh.UploadMeshData (false);
			return mesh;
		}

		void BuildShapeKeys (Transform skTrans, ConfigNode keyNode)
		{
			int keyCount = 0;
			if (keyNode != null) {
				keyCount = keyNode.values.Count;
			}
			var keys = new ShapeKey[keyCount];
			for (int i = 0; i < keys.Length; i++) {
				var value = keyNode.values[i];
				keys[i].name = value.name;
				float.TryParse (value.value, out keys[i].weight);
			}
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
				smr.sharedMesh = BuildShapeKeys (mf.sharedMesh, keys);
				smr.sharedMaterial = r.sharedMaterial;
				r.sharedMaterial = null;
				Destroy (mf);
				Destroy (r);
			} else {
				smr.sharedMesh = BuildShapeKeys (smr.sharedMesh, keys);
			}
			Debug.Log ($"[DeltaMu] {smr.sharedMesh.blendShapeCount}");
		}

		void BuildBlendShapes ()
		{
			ConfigNode keyNode = null;
			if (!String.IsNullOrEmpty (keyNodeString)) {
				keyNode = ConfigNode.Parse (keyNodeString).nodes[0];
			}
			int keyCount = 0;
			if (keyNode != null) {
				keyCount = keyNode.values.Count;
			}
			int index = 0;
			for (int i = 0; i < keyCount; i++) {
				var value = keyNode.values[i];
				if (!blendShapeDict.ContainsKey (value.name)) {
					var bs = new BlendShape(value.name, index++, shapeMesh);
					blendShapeDict[value.name] = bs;
					blendShapeList.Add (bs);
				}
			}
			while (index < shapeMesh.sharedMesh.blendShapeCount) {
				var bs = new BlendShape(index.ToString (), index++, shapeMesh);
				blendShapeList.Add (bs);
			}
		}

		//float startTime;
		SkinnedMeshRenderer shapeMesh;

		public override void OnStart (StartState state)
		{
			var skTrans = part.FindModelTransform (ShapeKeyTransform);
			if (skTrans == null) {
				Debug.Log ("[DeltaMu] {part.name}:{ShapeKeyTransform} not found");
				enabled = false;
				return;
			}
			shapeMesh = skTrans.gameObject.GetComponent<SkinnedMeshRenderer>();
			if (shapeMesh == null) {
				Debug.Log ("[DeltaMu] {part.name}:{ShapeKeyTransform} no SMR");
				enabled = false;
				return;
			}
			BuildBlendShapes ();
			for (int i = 0; i < blendShapeList.Count; i++) {
				var bf = blendShapeList[i].GetField ();
				Fields.Add (bf);
			}
		}
	}
}
