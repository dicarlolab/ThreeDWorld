using UnityEngine;
using System.Collections;
using System;

public class PlaneSpawn : SpawnArea {

	public override Vector3 acquireLocation() {
		System.Random rand = new System.Random ();

		Vector3 size = this.gameObject.GetComponent<Collider> ().bounds.size;
		Vector3 center = this.gameObject.GetComponent<Collider> ().bounds.center;

		float xMin = center.x - (.5f * size.x);
		float zMin = center.z - (.5f * size.z);
	
		float x = (float) (rand.NextDouble() * size.x) + xMin;
		float y = this.gameObject.transform.position.y;
		float z = (float) (rand.NextDouble () * size.z) + zMin;

		return new Vector3 (x, y, z);
	}

	public override Vector3 acquireLocation(int seed) {
		System.Random rand = new System.Random (seed);

		Vector3 size = this.gameObject.GetComponent<Collider> ().bounds.size;
		Vector3 center = this.gameObject.GetComponent<Collider> ().bounds.center;

		float xMin = center.x - (.5f * size.x);
		float zMin = center.z - (.5f * size.z);

		float x = (float) (rand.NextDouble() * size.x) + xMin;
		float y = this.gameObject.transform.position.y;
		float z = (float) (rand.NextDouble () * size.z) + zMin;

		return new Vector3 (x, y, z);
	}
}
