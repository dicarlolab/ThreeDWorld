using UnityEngine;
using System.Collections;

public class PointSpawn : SpawnArea {

	public override Vector3 acquireLocation() {
		return this.transform.position;
	}

	public override Vector3 acquireLocation(int seed) {
		return this.transform.position;
	}

}
