#pragma strict
@script ExecuteInEditMode()

var WoodObjects : GameObject[];
var MetalObjects : GameObject[];
var LeatherObjects : GameObject[];
//
var WoodMaterials : Material[];
var MetalMaterials : Material[];
var LeatherMaterials : Material[];


function ChangeMaterials () {
	var RandomWoodMat = Random.Range(0,WoodMaterials.Length);
	for (var WoodObj in WoodObjects) {
		WoodObj.GetComponent.<Renderer>().material = WoodMaterials[RandomWoodMat];
	}
	var RandomMetalMat = Random.Range(0,MetalMaterials.Length);
	for (var MetalObj in MetalObjects) {
		MetalObj.GetComponent.<Renderer>().material = MetalMaterials[RandomMetalMat];
	}
	var RandomLeatherMat = Random.Range(0,LeatherMaterials.Length);
	for (var LeatherObj in LeatherObjects) {
		LeatherObj.GetComponent.<Renderer>().material = LeatherMaterials[RandomLeatherMat];
	}
}
