using UnityEngine;
using System.Collections;

public abstract class SpawnArea : MonoBehaviour {

	public abstract Vector3 acquireLocation ();

	public abstract Vector3 acquireLocation (int seed);

}
