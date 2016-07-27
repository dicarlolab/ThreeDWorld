using UnityEngine;
using System.Collections;

public abstract class BenchmarkTest : MonoBehaviour {

#region Fields
	public string identifier = "";

	// max num of iterations
	protected int maxIterations;
	// index on current iteration
	protected int iterationCount;
	// list with domain covering iterations, range covering physics fps
	protected float[,] framerates;
	// num data points recorded so far current iteration
	protected int numDataPoints;
	// num times framerate avg is turned into a data point
	protected int snapshots;
	// num times an intermediate framerate is taken
	protected int intermediateSnapshots;
	// duration per iteration
	protected float iterationDuration;
	// time since last framerate was recorded
	protected float lastFrameRecordingTime;
	// intermediate framerates to be averaged to smooth out jumpy framerate data
	protected float[] intermediateFrameRates;
	// num intermediate recorded thus far per intermediate iteration
	protected int intermediateSnapshotCount;
	// time when current iteration started
	protected float iterationStartTime;
#endregion

#region Abstract Methods
	// clears world of everything related solely to last iteration
	public abstract void resetWorld();

	// calculates time for next iteration's duration
	public abstract float calculateDuration ();

	// obtains save path for benchmark data
	public abstract string getSavePath ();

	// sets up new iteration scenario
	public abstract void newIteration ();
#endregion

#region Virtual
	// writes data to a save path
	protected virtual void writeData () {
		if (iterationCount >= maxIterations) {
			string outputData = "";
			for (int i = 0; i < framerates.GetLength(0); i++) {
				for (int j = 0; j < framerates.GetLength(1); j++) {
					outputData += "(" + i + "," + framerates[i,j] + ");";
				}
			}
			System.IO.File.WriteAllText (@getSavePath (), outputData);
			print ("Data saved at: " + getSavePath ());
		}
	}
#endregion

#region Unity Callbacks
	// initializes data capture variables
	public virtual void Start () {
		iterationDuration = calculateDuration ();
		iterationStartTime = 0;

		snapshots = 10;
		intermediateSnapshots = 10;

		framerates = new float[maxIterations, snapshots];
		intermediateFrameRates = new float[intermediateSnapshots];

		iterationStartTime = Time.time;
		lastFrameRecordingTime = Time.time;
	}
		
	public virtual void FixedUpdate () {
		if (Time.time - iterationStartTime > iterationDuration && iterationCount < maxIterations) { // every new iteration

			iterationCount++;

			// setup new iteration
			resetWorld ();
			newIteration ();


			// set new fall duration to if benchmarkObject were 5*1.5 benchmarkObjects higher than current iteration
			iterationDuration = calculateDuration ();
			iterationStartTime = Time.time;

		} else if (numDataPoints < snapshots && Time.time - lastFrameRecordingTime > iterationDuration 
			/ snapshots && intermediateSnapshotCount == 10) { // every new snapshot per iteration

			float avgFrameRate = 0;
			for (int i = 0; i < snapshots; i++) {
				avgFrameRate += intermediateFrameRates [i];
			}
			avgFrameRate = avgFrameRate / snapshots;
			framerates [iterationCount, numDataPoints] = avgFrameRate;

			lastFrameRecordingTime = Time.time;
			numDataPoints++;

			intermediateSnapshotCount = 0;

		} else if (Time.time - lastFrameRecordingTime > iterationDuration / (snapshots * intermediateSnapshots) 
			* intermediateSnapshotCount && intermediateSnapshotCount < 10) { // every intermediate

			intermediateFrameRates [intermediateSnapshotCount] = Time.deltaTime;
			intermediateSnapshotCount++;

		}
	}
#endregion
}