using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

// just add empty game object and add script, currently you must have the tag 
//"sofa test" added to tags and have the sofa_blue object in scene
// essentially makes a bunch of couches fall on each other for use as a benchmark test
public class BenchmarkTesting : MonoBehaviour {
	
	// delta difference between num of couches per iteration of test
	private int deltaSofas;
	// object to drop on top of each other
	private GameObject sofaBlue;

	// max num of iterations
	private int maxIterations;
	// index on current iteration
	private int iterationCount;

	// list with domain covering number of sofas by delta sofas, range covering fps
	private float[,] framerates;
	// num data points recorded so far current iteration
	private int numDataPoints;
	// num times framerate avg is turned into a data point
	private int snapshots;
	// num times an intermediate framerate is taken
	private int intermediateSnapshots;

	// time since last framerate was recorded
	private float lastFrameRecordingTime;
	// intermediate framerates to be averaged to smooth out jumpy framerate data
	private float[] intermediateFrameRates;
	// num intermediate recorded thus far per intermediate iteration
	private int intermediateSnapshotCount;

	// initial coords, and rotation of sofa
	private Vector3[] initialSofaPosition = new Vector3[5];
	private Quaternion[] initialSofaRotation = new Quaternion[5];
	// height of sofa
	private float sofaHeight;
	// time it takes for current iteration to complete
	private float fallDuration;
	// distance between centers of sofas in sofaHeight units
	private float sofaScaledSeparation;
	// time when current iteration started
	private float iterationStartTime;

	// initializes instance variables
	void Start () {
		deltaSofas = 5;
		iterationCount = 0;
		maxIterations = 20;
		intermediateSnapshotCount = 0;

		sofaScaledSeparation = 1.5f;
		sofaBlue = GameObject.Find("sofa_blue");
		sofaHeight = 4.0f;

		for (int i = 0; i < 5; i++){
			initialSofaPosition[i] = sofaBlue.transform.GetChild (i).position;
			initialSofaRotation[i] = sofaBlue.transform.GetChild (i).rotation;
		}

		fallDuration = calculateFallDuration();
		iterationStartTime = 0;

		snapshots = 10;
		intermediateSnapshots = 10;

		framerates = new float[maxIterations, snapshots];
		intermediateFrameRates = new float[intermediateSnapshots];

		iterationStartTime = Time.time;
		lastFrameRecordingTime = Time.time;
	}

	// calculates time it would take the top couch to fall to the floor
	// and returns 2 times that duration.
	float calculateFallDuration () {
		int numSofas = deltaSofas * iterationCount;
		float totalHeight = numSofas * sofaHeight * sofaScaledSeparation;
		return 2.0f * Mathf.Pow((2.0f * totalHeight / (0-Physics.gravity.y)), .5f);
	}
	
	// Update is called once per frame
	void Update () {
		// if twice the time passed for highest couch to reach the floor,
		// run new iteration with deltaSofas sofas than last iteration unless
		// maxIterations
		if (Time.time - iterationStartTime > fallDuration && iterationCount < maxIterations) {

			iterationCount++;

			// reset main sofa to initial state
			ResetMainSofa ();

			// get rid of clone sofas
			foreach (GameObject obj in GameObject.FindGameObjectsWithTag("sofa test")) {
				Destroy (obj);
			}

			// create new sofa clones with tag "sofa test" indicating they are clones and place them some distance above each other
			for (int i = 0; i < deltaSofas * iterationCount; i++) {
				GameObject sofaBlueClone = Instantiate (sofaBlue);
				sofaBlueClone.tag = "sofa test";
				sofaBlueClone.transform.Translate (new Vector3 (0, (i + 1) * sofaHeight * sofaScaledSeparation), 0);
			}

			// set new fall duration to if sofa were 5*1.5 sofas higher than current iteration
			fallDuration = calculateFallDuration ();
			iterationStartTime = Time.time;

		} else if (numDataPoints < snapshots && Time.time - lastFrameRecordingTime > fallDuration / snapshots && intermediateSnapshotCount == 10) {
				
			float avgFrameRate = 0;
			for (int i = 0; i < snapshots; i++) {
				avgFrameRate += intermediateFrameRates [i];
			}
			avgFrameRate = avgFrameRate / snapshots;
			framerates [iterationCount, numDataPoints] = avgFrameRate;

			lastFrameRecordingTime = Time.time;
			numDataPoints++;

			intermediateSnapshotCount = 0;

		} else if (Time.time - lastFrameRecordingTime > fallDuration / (snapshots * intermediateSnapshots) * intermediateSnapshotCount && intermediateSnapshotCount < 10) {
			
			intermediateFrameRates [intermediateSnapshotCount] = Time.deltaTime;
			intermediateSnapshotCount++;
		
		}

		if (iterationCount >= maxIterations) {
			string outputData = "";
			for (int i = 0; i < framerates.GetLength(0); i++) {
				for (int j = 0; j < framerates.GetLength(1); j++) {
					outputData += "(" + ((i + 1) * deltaSofas) + "," + (framerates[i,j]) + ");";
				}
			}
			string savePath = "";
			System.IO.File.WriteAllText (@savePath, outputData);
		}
	}

	// puts the sofa back to initial position and rotation with no velocities
	void ResetMainSofa() {
		for (int i = 0; i < 5; i++) {
			Rigidbody rb = sofaBlue.transform.GetChild(i).GetComponent<Rigidbody> ();
			sofaBlue.transform.GetChild(i).position = initialSofaPosition[i];
			sofaBlue.transform.GetChild(i).rotation = initialSofaRotation[i];
			if (rb != null) {
				rb.velocity = Vector3.zero;
				rb.angularVelocity = Vector3.zero;
			} else {
				Debug.LogError("Main Sofa Rigidbody does not exist :(");
			}
		}
	}
}
