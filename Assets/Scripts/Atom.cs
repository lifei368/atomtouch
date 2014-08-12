using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;

//L-J potentials from Zhen and Davies, Phys. Stat. Sol. a, 78, 595 (1983)
//Symbol, epsilon/k_Boltzmann (K) n-m version, 12-6 version, sigma (Angstroms),
//     mass in amu, mass in (20 amu) for Unity 
//     FCC lattice parameter in Angstroms, expected NN bond (Angs)
//Au: 4683.0, 5152.9, 2.6367, 196.967, 9.848, 4.080, 2.88
//Cu: 3401.1, 4733.5, 2.3374,  63.546, 3.177, 3.610, 2.55
//Pt: 7184.2, 7908.7, 2.5394, 165.084, 8.254, 3.920, 2.77

public abstract class Atom : MonoBehaviour
{
	private Vector3 offset;
	private Vector3 screenPoint;
	private Vector3 lastMousePosition;
	private Vector3 lastTouchPosition;
	private GameObject moleculeToMove = null;
	private float deltaTouch2 = 0.0f;
	private bool moveZDirection = false;
	private float lastTapTime;
	private float tapTime = .35f;
	[HideInInspector]public bool selected = false;
	[HideInInspector]public bool doubleTapped = false;
	private Dictionary<String, Vector3> gameObjectOffsets;
	private Dictionary<String, Vector3> gameObjectScreenPoints;
	private TextMesh angstromText;
	private Vector3 velocityBeforeCollision;
	private float dragStartTime;
	private bool dragCalled;
	private Dictionary<String, TextMesh> bondDistanceText;
	
	public Material lineMaterial;
	public TextMesh textMeshPrefab;
	public bool held { get; set; }

	//variables that must be implemented because they are declared as abstract in the base class
	public abstract float epsilon{ get; } // J
	public abstract float sigma { get; }
	protected abstract float massamu{ get; } //amu
	protected abstract void SetSelected (bool selected);
	public abstract void SetTransparent (bool transparent);
	public abstract String atomName { get; }
	
	private Vector3 lastVelocity = Vector3.zero;
	private Vector3 a_n = Vector3.zero;
	private Vector3 a_nplus1 = Vector3.zero;

	void Awake(){

		gameObject.rigidbody.velocity = new Vector3 (UnityEngine.Random.Range(-1.0f, 1.0f), UnityEngine.Random.Range(-1.0f, 1.0f), UnityEngine.Random.Range(-1.0f, 1.0f));
		bondDistanceText = new Dictionary<String, TextMesh> ();
	}

	void FixedUpdate(){
		if (!StaticVariables.pauseTime) {
			GameObject[] allMolecules = GameObject.FindGameObjectsWithTag("Molecule");
			List<GameObject> molecules = new List<GameObject>();
			
			for(int i = 0; i < allMolecules.Length; i++){
				double distance = Vector3.Distance(transform.position, allMolecules[i].transform.position);
				if(allMolecules[i] != gameObject && distance < (StaticVariables.cutoff)){
					molecules.Add(allMolecules[i]);
				}
			}

			//Here is where the potentials will need to swapped out
			//when changing the currentPotential variable, make sure you reset the system
			Vector3 force = Vector3.zero;
			if(StaticVariables.currentPotential == StaticVariables.Potential.LennardJones){
				force = GetLennardJonesForce (molecules);
			}
			else if(StaticVariables.currentPotential == StaticVariables.Potential.Brenner){
				force = GetLennardJonesForce (molecules);
			}
			else{
				force = GetLennardJonesForce (molecules);
			}



			if(!gameObject.rigidbody.isKinematic) gameObject.rigidbody.angularVelocity = Vector3.zero;

			gameObject.rigidbody.AddForce (force, mode:ForceMode.Force);

			Vector3 newVelocity = gameObject.rigidbody.velocity * TemperatureCalc.squareRootAlpha;
			if ((rigidbody.velocity.magnitude != 0) && !rigidbody.isKinematic && !float.IsInfinity(TemperatureCalc.squareRootAlpha) && allMolecules.Length > 1) {
				gameObject.rigidbody.velocity = newVelocity;
			}

		}
		else{
			GameObject[] allMolecules = GameObject.FindGameObjectsWithTag("Molecule");
			for(int i = 0; i < allMolecules.Length; i++){
				GameObject currAtom = allMolecules[i];
				if(!currAtom.rigidbody.isKinematic){
					currAtom.rigidbody.velocity = new Vector3(0.0f, 0.0f, 0.0f);
				}
			}
		}

	}

	Vector3 GetLennardJonesForce(List<GameObject> objectsInRange){
		//double startTime = Time.realtimeSinceStartup;
		Vector3 finalForce = new Vector3 (0.000f, 0.000f, 0.000f);
		for (int i = 0; i < objectsInRange.Count; i++) {
			Vector3 direction = new Vector3(objectsInRange[i].transform.position.x - transform.position.x, objectsInRange[i].transform.position.y - transform.position.y, objectsInRange[i].transform.position.z - transform.position.z);
			direction.Normalize();

			Atom otherAtomScript = objectsInRange[i].GetComponent<Atom>();
			float finalSigma = StaticVariables.sigmaValues[atomName+otherAtomScript.atomName];
			//TTM add transition to smooth curve to constant, instead of asymptote to infinity
			double r_min = StaticVariables.r_min_multiplier * finalSigma;

			double distance = Vector3.Distance(transform.position, objectsInRange[i].transform.position);
			double distanceMeters = distance * StaticVariables.angstromsToMeters; //distance in meters, though viewed in Angstroms
			double magnitude = 0.0;

			if(distance > r_min){
				double part1 = ((-48 * epsilon) / Math.Pow(distanceMeters, 2));
				double part2 = (Math.Pow ((finalSigma / distance), 12) - (.5f * Math.Pow ((finalSigma / distance), 6)));
				magnitude = (part1 * part2 * distanceMeters);
			}
			else{
				double r_min_meters = r_min * StaticVariables.angstromsToMeters;
				double V_rmin_part1 = ((-48 * epsilon) / Math.Pow(r_min_meters, 2));
				double V_rmin_part2 = (Math.Pow ((finalSigma / r_min), 12) - (.5f * Math.Pow ((finalSigma / r_min), 6)));
				double V_rmin_magnitude = (V_rmin_part1 * V_rmin_part2 * r_min_meters);

				double r_Vmax = StaticVariables.r_min_multiplier * finalSigma/1.5;
				double r_Vmax_meters = r_Vmax * StaticVariables.angstromsToMeters;
				double Vmax_part1 = ((-48 * epsilon) / Math.Pow(r_Vmax_meters, 2));
				double Vmax_part2 = (Math.Pow ((finalSigma / r_Vmax), 12) - (.5f * Math.Pow ((finalSigma / r_Vmax), 6)));
				double Vmax_magnitude = (Vmax_part1 * Vmax_part2 * r_Vmax_meters);

				double part1 = (distance/r_min)*(Math.Exp (distance)/Math.Exp (r_min));
				double part2 = Vmax_magnitude - V_rmin_magnitude;
				magnitude = Vmax_magnitude - (part1* part2);
			}
			finalForce += (direction * (float)magnitude);
			//double endTime = Time.realtimeSinceStartup;
			//print ("elapsedTime: " + (endTime - startTime));
		}

		Vector3 adjustedForce = finalForce / StaticVariables.mass100amuToKg;
		adjustedForce = adjustedForce / StaticVariables.angstromsToMeters;
		adjustedForce = adjustedForce * StaticVariables.fixedUpdateIntervalToRealTime * StaticVariables.fixedUpdateIntervalToRealTime;
		return adjustedForce;
	}



	void Update(){
		if (Application.platform == RuntimePlatform.IPhonePlayer) {
			if(Input.touchCount > 0){
				Ray ray = Camera.main.ScreenPointToRay(Input.GetTouch(0).position);
				RaycastHit hitInfo;
				if(!held && Physics.Raycast(ray, out hitInfo) && hitInfo.transform.gameObject.tag == "Molecule" && hitInfo.transform.gameObject == gameObject){
					if(Input.GetTouch(0).phase == TouchPhase.Began){
						OnMouseDownIOS();
					}
				}
				else if(held){
					if(Input.GetTouch(0).phase == TouchPhase.Moved && Input.touchCount == 1){
						OnMouseDragIOS();
					}
					else if(Input.touchCount == 2){
						//handle z axis movement
						HandleZAxisTouch();
					}
					else if(Input.GetTouch(0).phase == TouchPhase.Canceled || Input.GetTouch(0).phase == TouchPhase.Ended){
						OnMouseUpIOS();
					}
					lastTouchPosition = Input.GetTouch(0).position;
				}
			}
		}
		else{
			if(Input.GetMouseButtonDown(0)){
				if((Time.realtimeSinceStartup - lastTapTime) < tapTime){
					ResetDoubleTapped();
					doubleTapped = true;
					RemoveAllBondText();
				}
				Ray ray = Camera.main.ScreenPointToRay( Input.mousePosition );
				RaycastHit hitInfo;
				if (Physics.Raycast( ray, out hitInfo ) && hitInfo.transform.gameObject.tag == "Molecule" && hitInfo.transform.gameObject == gameObject){
					lastTapTime = Time.realtimeSinceStartup;
				}
			}
			
			HandleRightClick();
		}
		if (doubleTapped) {
			Time.timeScale = .05f;
			CameraScript cameraScript = Camera.main.GetComponent<CameraScript>();
			cameraScript.setCameraCoordinates(transform);
			UpdateBondText();
			ApplyTransparency();
		}
		CheckVelocity ();
	}

	void HandleRightClick(){
		if (Input.GetMouseButtonDown (1)) {
			Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
			RaycastHit hitInfo;
			if(Physics.Raycast(ray, out hitInfo) && hitInfo.transform.gameObject.tag == "Molecule" && hitInfo.transform.gameObject == gameObject){
				selected = !selected;
				SetSelected(selected);
			}
		}
	}

	//controls for touch devices
	void HandleZAxisTouch(){
		if(Input.touchCount == 2){
			Touch touch2 = Input.GetTouch(1);
			if(touch2.phase == TouchPhase.Began){
				moveZDirection = true;
			}
			else if(touch2.phase == TouchPhase.Moved){
				if(!selected){
					Vector2 touchOnePrevPos = touch2.position - touch2.deltaPosition;
					float deltaMagnitudeDiff = touch2.position.y - touchOnePrevPos.y;
					deltaTouch2 = deltaMagnitudeDiff / 10.0f;
					Quaternion cameraRotation = Camera.main.transform.rotation;
					Vector3 projectPosition = transform.position;
					projectPosition += (cameraRotation * new Vector3(0.0f, 0.0f, deltaTouch2));
					transform.position = CheckPosition(projectPosition);
					screenPoint += new Vector3(0.0f, 0.0f, deltaTouch2);
				}
				else{
//					Vector2 touchOnePrevPos = touch2.position - touch2.deltaPosition;
//					float deltaMagnitudeDiff = touch2.position.y - touchOnePrevPos.y;
//					deltaTouch2 = deltaMagnitudeDiff / 10.0f;
//					GameObject[] allMolecules = GameObject.FindGameObjectsWithTag("Molecule");
//					List<Vector3> atomPositions = new List<Vector3>();
//					bool moveAtoms = true;
//					for(int i = 0; i < allMolecules.Length; i++){
//						GameObject currAtom = allMolecules[i];
//						Quaternion cameraRotation = Camera.main.transform.rotation;
//						Vector3 projectPosition = currAtom.transform.position;
//						projectPosition += (cameraRotation * new Vector3(0.0f, 0.0f, deltaTouch2));
//						Vector3 newAtomPosition = CheckPosition(projectPosition);
//						if(newAtomPosition != projectPosition){
//							moveAtoms = false;
//						}
//						if(gameObjectScreenPoints != null){
//							gameObjectScreenPoints[currAtom.name] += new Vector3(0.0f, 0.0f, deltaTouch2);
//						}
//						atomPositions.Add(newAtomPosition);
//					}
//
//					if(atomPositions.Count > 0 && moveAtoms){
//						for(int i = 0; i < allMolecules.Length; i++){
//							GameObject currAtom = allMolecules[i];
//							Vector3 newAtomPosition = atomPositions[i];
//							currAtom.transform.position = newAtomPosition;
//						}
//					}
				}
			}
		}
		else if(Input.touchCount == 0 && moveZDirection){
			moveZDirection = false;
			moleculeToMove = null;
			held = false;
			GameObject[] allMolecules = GameObject.FindGameObjectsWithTag("Molecule");
			for(int i = 0; i < allMolecules.Length; i++){
				GameObject currAtom = allMolecules[i];
				Atom atomScript = currAtom.GetComponent<Atom>();
				atomScript.SetSelected(atomScript.selected);
			}
			
		}
	}

	void ResetDoubleTapped(){
		GameObject[] allMolecules = GameObject.FindGameObjectsWithTag("Molecule");
		for (int i = 0; i < allMolecules.Length; i++) {
			Atom atomScript = allMolecules[i].GetComponent<Atom>();
			atomScript.doubleTapped = false;
		}
	}

	void HandleMovingAtom(){
		Touch touch = Input.GetTouch(0);

		if(touch.phase == TouchPhase.Began){
			if((Time.time - lastTapTime) < tapTime){
				ResetDoubleTapped();
				doubleTapped = true;
				RemoveAllBondText();
			}
			Ray ray = Camera.main.ScreenPointToRay( Input.touches[0].position );
			RaycastHit hitInfo;
			//this is the iOS equivalent to OnMouseUp
			if (Physics.Raycast( ray, out hitInfo ) && hitInfo.transform.gameObject.tag == "Molecule" && hitInfo.transform.gameObject == gameObject)
			{
				dragStartTime = Time.realtimeSinceStartup;
				dragCalled = false;
				if(!selected){
					moleculeToMove = gameObject;
					screenPoint = Camera.main.WorldToScreenPoint(transform.position);
					offset = moleculeToMove.transform.position - Camera.main.ScreenToWorldPoint(new Vector3(Input.GetTouch(0).position.x, Input.GetTouch(0).position.y - 50, screenPoint.z));
					held = true;
					rigidbody.isKinematic = true;
				}
				else{
					GameObject[] allMolecules = GameObject.FindGameObjectsWithTag("Molecule");
					gameObjectOffsets = new Dictionary<String, Vector3>();
					gameObjectScreenPoints = new Dictionary<String, Vector3>();
					for(int i = 0; i < allMolecules.Length; i++){
						GameObject currAtom = allMolecules[i];
						Atom atomScript = currAtom.GetComponent<Atom>();
						if(atomScript.selected){
							currAtom.rigidbody.isKinematic = true;
							Vector3 pointOnScreen = Camera.main.WorldToScreenPoint(currAtom.transform.position);
							Vector3 atomOffset = currAtom.transform.position - Camera.main.ScreenToWorldPoint(
								new Vector3(Input.GetTouch(0).position.x, Input.GetTouch(0).position.y - 15.0f, pointOnScreen.z));
							held = true;
							//print ("adding key: " + currAtom.name);
							gameObjectOffsets.Add(currAtom.name, atomOffset);
							gameObjectScreenPoints.Add(currAtom.name, pointOnScreen);
						}
					}
				}
				lastTapTime = Time.time;
			}
		}
		//this is the iOS equivalent to OnMouseDrag
		else if(touch.phase == TouchPhase.Moved){

			if(Time.realtimeSinceStartup - dragStartTime > 0.1f){

				if(!selected){
					if(moleculeToMove != null && !doubleTapped){
						dragCalled = true;
						Vector3 curScreenPoint = new Vector3(Input.GetTouch(0).position.x, Input.GetTouch(0).position.y, screenPoint.z);
						Vector3 curPosition = Camera.main.ScreenToWorldPoint(curScreenPoint) + offset;
						lastMousePosition = new Vector3(Input.GetTouch(0).position.x, Input.GetTouch(0).position.y, 0.0f);
						curPosition = CheckPosition(curPosition);
						moleculeToMove.transform.position = curPosition;
						//ApplyTransparency();
					}
				}
				else{
//					if (held){
//						GameObject[] allMolecules = GameObject.FindGameObjectsWithTag("Molecule");
//						List<Vector3> atomPositions = new List<Vector3>();
//						bool moveAtoms = true;
//						for(int i = 0; i < allMolecules.Length; i++){
//							GameObject currAtom = allMolecules[i];
//							Atom atomScript = currAtom.GetComponent<Atom>();
//							if(atomScript.selected){
//								if(gameObjectOffsets != null && gameObjectScreenPoints != null){
//									Vector3 currScreenPoint = gameObjectScreenPoints[currAtom.name];
//									Vector3 currOffset = gameObjectOffsets[currAtom.name];
//									Vector3 objScreenPoint = new Vector3(Input.GetTouch(0).position.x, Input.GetTouch(0).position.y, currScreenPoint.z);
//									Vector3 curPosition = Camera.main.ScreenToWorldPoint(objScreenPoint) + currOffset;
//									Vector3 newAtomPosition = CheckPosition(curPosition);
//									if(newAtomPosition != curPosition){
//										moveAtoms = false;
//									}
//									atomPositions.Add(newAtomPosition);
//								}
//							}
//						}
//						
//						if(atomPositions.Count > 0 && moveAtoms){
//							for(int i = 0; i < allMolecules.Length; i++){
//								GameObject currAtom = allMolecules[i];
//								Vector3 newAtomPosition = atomPositions[i];
//								currAtom.transform.position = newAtomPosition;
//							}
//						}
//					}
				}
			}


		}
		//this is the iOS equivalent to OnMouseUp
		else if(touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled){
			if(!dragCalled && held){
				selected = !selected;
				SetSelected(selected);
			}
			GameObject[] allMolecules = GameObject.FindGameObjectsWithTag("Molecule");
			moleculeToMove = null;
			if(!selected){
				if(moleculeToMove != null){
					//Quaternion cameraRotation = Camera.main.transform.rotation;
					rigidbody.isKinematic = false;
					//rigidbody.AddForce (cameraRotation * mouseDelta * 50.0f);
					held = false;
				}
			}
			else{
				for(int i = 0; i < allMolecules.Length; i++){
					GameObject currAtom = allMolecules[i];
					Atom atomScript = currAtom.GetComponent<Atom>();
					if(atomScript.selected){
						currAtom.rigidbody.isKinematic = false;
						atomScript.held = false;
					}
				}
			}

			for(int i = 0; i < allMolecules.Length; i++){
				GameObject currAtom = allMolecules[i];
				Atom atomScript = currAtom.GetComponent<Atom>();
				atomScript.SetSelected(atomScript.selected);
			}
		}
	}

	void SpawnAngstromText(){
		Quaternion cameraRotation = Camera.main.transform.rotation;
		Vector3 up = cameraRotation * Vector3.up;
		Vector3 left = cameraRotation * -Vector3.right;
		angstromText = Instantiate(textMeshPrefab, new Vector3(0.0f, 0.0f, 0.0f), cameraRotation) as TextMesh;
		angstromText.renderer.material.renderQueue = StaticVariables.overlay;
		Vector3 newPosition = transform.position + (left * 1.0f) + (up * 2.0f);
		angstromText.transform.position = newPosition;
		angstromText.text = "1 Angstrom";
		LineRenderer angstromLine = angstromText.transform.gameObject.AddComponent<LineRenderer> ();
		angstromLine.material = lineMaterial;
		angstromLine.SetColors(Color.yellow, Color.yellow);
		angstromLine.SetWidth(0.2F, 0.2F);
		angstromLine.SetVertexCount(2);
	}

	void MoveAngstromText(){
		Quaternion cameraRotation = Camera.main.transform.rotation;
		Vector3 up = cameraRotation * Vector3.up;
		Vector3 left = cameraRotation * -Vector3.right;
		Vector3 newPosition = transform.position + (left * 1.0f) + (up * 2.0f);
		if (angstromText != null) {
			angstromText.transform.position = newPosition;
			LineRenderer angstromLine = angstromText.GetComponent<LineRenderer> ();
			Vector3 position1 = transform.position + (left * .5f) + (up);
			Vector3 position2 = transform.position + (left * -.5f) + (up);
			angstromLine.SetPosition(0, position1);
			angstromLine.SetPosition(1, position2);
		}
	}

	void DestroyAngstromText(){
		if (angstromText != null) {
			LineRenderer angstromLine = angstromText.GetComponent<LineRenderer> ();
			Destroy(angstromLine);
			Destroy(angstromText);
		}
	}

	void OnMouseDownIOS(){
		dragStartTime = Time.realtimeSinceStartup;
		dragCalled = false;
		held = true;
		if (!selected) {
			rigidbody.isKinematic = true;
			screenPoint = Camera.main.WorldToScreenPoint(transform.position);
			offset = transform.position - Camera.main.ScreenToWorldPoint(
				new Vector3(Input.GetTouch(0).position.x, Input.GetTouch(0).position.y - 15.0f, screenPoint.z));
		}
		else{
			GameObject[] allMolecules = GameObject.FindGameObjectsWithTag("Molecule");
			gameObjectOffsets = new Dictionary<String, Vector3>();
			gameObjectScreenPoints = new Dictionary<String, Vector3>();
			for(int i = 0; i < allMolecules.Length; i++){
				GameObject currAtom = allMolecules[i];
				Atom atomScript = currAtom.GetComponent<Atom>();
				if(atomScript.selected){
					currAtom.rigidbody.isKinematic = true;
					Vector3 pointOnScreen = Camera.main.WorldToScreenPoint(currAtom.transform.position);
					Vector3 atomOffset = currAtom.transform.position - Camera.main.ScreenToWorldPoint(
						new Vector3(Input.GetTouch(0).position.x, Input.GetTouch(0).position.y - 15.0f, pointOnScreen.z));
					atomScript.held = true;
					gameObjectOffsets.Add(currAtom.name, atomOffset);
					gameObjectScreenPoints.Add(currAtom.name, pointOnScreen);
				}
			}
		}
	}
	
	//controls for debugging on pc
	void OnMouseDown (){
		if (Application.platform != RuntimePlatform.IPhonePlayer) {
			dragStartTime = Time.realtimeSinceStartup;
			dragCalled = false;
			held = true;

			if(!selected){
				rigidbody.isKinematic = true;
				screenPoint = Camera.main.WorldToScreenPoint(transform.position);
				offset = transform.position - Camera.main.ScreenToWorldPoint(
					new Vector3(Input.mousePosition.x, Input.mousePosition.y - 15.0f, screenPoint.z));

			}
			else{
				GameObject[] allMolecules = GameObject.FindGameObjectsWithTag("Molecule");
				gameObjectOffsets = new Dictionary<String, Vector3>();
				gameObjectScreenPoints = new Dictionary<String, Vector3>();
				for(int i = 0; i < allMolecules.Length; i++){
					GameObject currAtom = allMolecules[i];
					Atom atomScript = currAtom.GetComponent<Atom>();
					if(atomScript.selected){
						currAtom.rigidbody.isKinematic = true;
						Vector3 pointOnScreen = Camera.main.WorldToScreenPoint(currAtom.transform.position);
						Vector3 atomOffset = currAtom.transform.position - Camera.main.ScreenToWorldPoint(
							new Vector3(Input.mousePosition.x, Input.mousePosition.y - 15.0f, pointOnScreen.z));
						atomScript.held = true;
						//print ("adding key: " + currAtom.name);
						gameObjectOffsets.Add(currAtom.name, atomOffset);
						gameObjectScreenPoints.Add(currAtom.name, pointOnScreen);
					}
				}
			}
		}
	}

	void OnMouseDragIOS(){
		if (Time.realtimeSinceStartup - dragStartTime > 0.1f) {
			dragCalled = true;
			Quaternion cameraRotation = Camera.main.transform.rotation;
			ApplyTransparency();
			if(!selected){
				Vector3 diffVector = new Vector3(lastTouchPosition.x, lastTouchPosition.y) - new Vector3(Input.GetTouch(0).position.x, Input.GetTouch(0).position.y);
				if(diffVector.magnitude > 0 && !doubleTapped && Input.touchCount == 1){
					Vector3 curScreenPoint = new Vector3(Input.GetTouch(0).position.x, Input.GetTouch(0).position.y, screenPoint.z);
					Vector3 curPosition = Camera.main.ScreenToWorldPoint(curScreenPoint) + offset;
					curPosition = CheckPosition(curPosition);
					transform.position = curPosition;
				}
			}
			else{
				GameObject[] allMolecules = GameObject.FindGameObjectsWithTag("Molecule");
				bool noneDoubleTapped = true;
				for(int i = 0; i < allMolecules.Length; i++){
					GameObject currAtom = allMolecules[i];
					Atom atomScript = currAtom.GetComponent<Atom>();
					if(atomScript.doubleTapped && atomScript.selected) noneDoubleTapped = false;
				}

				if(noneDoubleTapped){
					List<Vector3> atomPositions = new List<Vector3>();
					bool moveAtoms = true;
					for(int i = 0; i < allMolecules.Length; i++){
						GameObject currAtom = allMolecules[i];
						Atom atomScript = currAtom.GetComponent<Atom>();
						Vector3 newAtomPosition = currAtom.transform.position;
						Vector3 diffVector = new Vector3(lastTouchPosition.x, lastTouchPosition.y) - new Vector3(Input.GetTouch(0).position.x, Input.GetTouch(0).position.y);
						if(diffVector.magnitude > 0 && !doubleTapped && atomScript.selected && Input.touchCount == 1){
							if(gameObjectOffsets != null && gameObjectScreenPoints != null){
								Vector3 currScreenPoint = gameObjectScreenPoints[currAtom.name];
								Vector3 currOffset = gameObjectOffsets[currAtom.name];
								Vector3 objScreenPoint = new Vector3(Input.GetTouch(0).position.x, Input.GetTouch(0).position.y, currScreenPoint.z);
								Vector3 curPosition = Camera.main.ScreenToWorldPoint(objScreenPoint) + currOffset;
								newAtomPosition = CheckPosition(curPosition);
								if(newAtomPosition != curPosition){
									moveAtoms = false;
								}
							}
						}
						Vector3 finalPosition = newAtomPosition;
						atomPositions.Add(finalPosition);
					}
					if(atomPositions.Count > 0 && moveAtoms){
						for(int i = 0; i < allMolecules.Length; i++){
							Vector3 newAtomPosition = atomPositions[i];
							GameObject currAtom = allMolecules[i];
							currAtom.transform.position = newAtomPosition;
						}
					}
				}
			}
		}
	}
	
	void OnMouseDrag(){
		if (Application.platform != RuntimePlatform.IPhonePlayer) {

			if(Time.realtimeSinceStartup - dragStartTime > 0.1f){
				dragCalled = true;
				Quaternion cameraRotation = Camera.main.transform.rotation;
				ApplyTransparency();
				//held = true;

				if(!selected){
					if((lastMousePosition - Input.mousePosition).magnitude > 0 && !doubleTapped){
						Vector3 curScreenPoint = new Vector3(Input.mousePosition.x, Input.mousePosition.y, screenPoint.z);
						Vector3 curPosition = Camera.main.ScreenToWorldPoint(curScreenPoint) + offset;
						curPosition = CheckPosition(curPosition);
						transform.position = curPosition;
					}
					
					float deltaZ = -Input.GetAxis("Mouse ScrollWheel");
					Vector3 projectPosition = transform.position;
					projectPosition += (cameraRotation * new Vector3(0.0f, 0.0f, deltaZ));
					transform.position = CheckPosition(projectPosition);
					screenPoint += new Vector3(0.0f, 0.0f, deltaZ);
				}
				else{
					GameObject[] allMolecules = GameObject.FindGameObjectsWithTag("Molecule");
					bool noneDoubleTapped = true;
					for(int i = 0; i < allMolecules.Length; i++){
						GameObject currAtom = allMolecules[i];
						Atom atomScript = currAtom.GetComponent<Atom>();
						if(atomScript.doubleTapped && atomScript.selected) noneDoubleTapped = false;
					}
					
					if(noneDoubleTapped){
						List<Vector3> atomPositions = new List<Vector3>();
						bool moveAtoms = true;
						for(int i = 0; i < allMolecules.Length; i++){
							GameObject currAtom = allMolecules[i];
							Atom atomScript = currAtom.GetComponent<Atom>();
							Vector3 newAtomPosition = currAtom.transform.position;
							if((lastMousePosition - Input.mousePosition).magnitude > 0 && atomScript.selected){
								Vector3 currScreenPoint = gameObjectScreenPoints[currAtom.name];
								Vector3 currOffset = gameObjectOffsets[currAtom.name];
								Vector3 objScreenPoint = new Vector3(Input.mousePosition.x, Input.mousePosition.y, currScreenPoint.z);
								Vector3 curPosition = Camera.main.ScreenToWorldPoint(objScreenPoint) + currOffset;
								newAtomPosition = CheckPosition(curPosition);
								if(newAtomPosition != curPosition){
									moveAtoms = false;
								}
								//currAtom.transform.position = newAtomPosition;
							}
							
							Vector3 finalPosition = newAtomPosition;
							
							if(atomScript.selected){
								float deltaZ = -Input.GetAxis("Mouse ScrollWheel");
								Vector3 projectPosition = newAtomPosition;
								projectPosition += (cameraRotation * new Vector3(0.0f, 0.0f, deltaZ));
								finalPosition = CheckPosition(projectPosition);
								gameObjectScreenPoints[currAtom.name] += new Vector3(0.0f, 0.0f, deltaZ);
								if(finalPosition != projectPosition){
									moveAtoms = false;
								}
							}
							atomPositions.Add(finalPosition);
						}
						
						if(atomPositions.Count > 0 && moveAtoms){
							for(int i = 0; i < allMolecules.Length; i++){
								Vector3 newAtomPosition = atomPositions[i];
								GameObject currAtom = allMolecules[i];
								currAtom.transform.position = newAtomPosition;
							}
						}
					}
				}
			}
			
			
			lastMousePosition = Input.mousePosition;
		}
		
	}

	void OnMouseUpIOS(){
		if (!dragCalled) {
			selected = !selected;
			SetSelected(selected);
		}
		else{
			GameObject[] allMolecules = GameObject.FindGameObjectsWithTag("Molecule");

			if(!selected){
				rigidbody.isKinematic = false;

				Quaternion cameraRotation = Camera.main.transform.rotation;
				Vector3 direction = (new Vector3(Input.GetTouch(0).position.x, Input.GetTouch(0).position.y, 0.0f) - new Vector3(lastTouchPosition.x, lastTouchPosition.y, 0.0f));
				float directionMagnitude = direction.magnitude;
				direction.Normalize();
				float magnitude = 2.0f * directionMagnitude;
				Vector3 flingVector = magnitude * new Vector3(direction.x, direction.y, 0.0f);
				gameObject.rigidbody.velocity = flingVector;
			}
			else{
				for(int i = 0; i < allMolecules.Length; i++){
					GameObject currAtom = allMolecules[i];
					Atom atomScript = currAtom.GetComponent<Atom>();
					if(atomScript.selected){
						currAtom.rigidbody.isKinematic = false;
						atomScript.held = false;

						Quaternion cameraRotation = Camera.main.transform.rotation;
						Vector3 direction = (new Vector3(Input.GetTouch(0).position.x, Input.GetTouch(0).position.y, 0.0f) - new Vector3(lastTouchPosition.x, lastTouchPosition.y, 0.0f));
						float directionMagnitude = direction.magnitude;
						direction.Normalize();
						float magnitude = 2.0f * directionMagnitude;
						Vector3 flingVector = magnitude * new Vector3(direction.x, direction.y, 0.0f);
						currAtom.rigidbody.velocity = flingVector;
					}
				}
			}
			
			for(int i = 0; i < allMolecules.Length; i++){
				GameObject currAtom = allMolecules[i];
				Atom atomScript = currAtom.GetComponent<Atom>();
				atomScript.SetSelected(atomScript.selected);
			}

		}
		held = false;
	}
	
	void OnMouseUp (){
		if (Application.platform != RuntimePlatform.IPhonePlayer) {
			if(!dragCalled){
				selected = !selected;
				SetSelected(selected);
			}
			else{
				GameObject[] allMolecules = GameObject.FindGameObjectsWithTag("Molecule");

				if(!selected){
					rigidbody.isKinematic = false;

					Quaternion cameraRotation = Camera.main.transform.rotation;
					Vector2 direction = (Input.mousePosition - lastMousePosition);
					direction.Normalize();
					float magnitude = 10.0f;
					Vector3 flingVector = magnitude * new Vector3(direction.x, direction.y, 0.0f);
					gameObject.rigidbody.velocity = flingVector;
				}
				else{
					for(int i = 0; i < allMolecules.Length; i++){
						GameObject currAtom = allMolecules[i];
						Atom atomScript = currAtom.GetComponent<Atom>();
						if(atomScript.selected){
							currAtom.rigidbody.isKinematic = false;
							atomScript.held = false;

							Quaternion cameraRotation = Camera.main.transform.rotation;
							Vector3 direction = (Input.mousePosition - lastMousePosition);
							direction.Normalize();
							float magnitude = 10.0f;
							Vector3 flingVector = magnitude * new Vector3(direction.x, direction.y, 0.0f);
							currAtom.rigidbody.velocity = flingVector;
						}
					}
				}
				
				for(int i = 0; i < allMolecules.Length; i++){
					GameObject currAtom = allMolecules[i];
					Atom atomScript = currAtom.GetComponent<Atom>();
					atomScript.SetSelected(atomScript.selected);
				}
			}
			held = false;
		}
	}


	public float BondDistance(GameObject otherAtom){
		Atom otherAtomScript = otherAtom.GetComponent<Atom> ();
		return 1.225f * StaticVariables.sigmaValues [atomName+otherAtomScript.atomName];
	}


	void CheckVelocity(){

		if (gameObject.rigidbody.isKinematic) return;

		CreateEnvironment createEnvironment = Camera.main.GetComponent<CreateEnvironment> ();
		Vector3 bottomPlanePos = createEnvironment.bottomPlane.transform.position;
		Vector3 newVelocity = gameObject.rigidbody.velocity;
		if (gameObject.transform.position.x > bottomPlanePos.x + (createEnvironment.width / 2.0f) - createEnvironment.errorBuffer) {
			newVelocity.x = Math.Abs(newVelocity.x) * -1;
		}
		if (gameObject.transform.position.x < bottomPlanePos.x - (createEnvironment.width / 2.0f) + createEnvironment.errorBuffer) {
			newVelocity.x = Math.Abs(newVelocity.x);
		}
		if (gameObject.transform.position.y > bottomPlanePos.y + (createEnvironment.height) - createEnvironment.errorBuffer) {
			newVelocity.y = Math.Abs(newVelocity.y) * -1;
		}
		if (gameObject.transform.position.y < bottomPlanePos.y + createEnvironment.errorBuffer) {
			newVelocity.y = Math.Abs(newVelocity.y);
		}
		if (gameObject.transform.position.z > bottomPlanePos.z + (createEnvironment.depth / 2.0f) - createEnvironment.errorBuffer) {
			newVelocity.z = Math.Abs(newVelocity.z) * -1;
		}
		if (gameObject.transform.position.z < bottomPlanePos.z - (createEnvironment.depth / 2.0f) + createEnvironment.errorBuffer) {
			newVelocity.z = Math.Abs(newVelocity.z);
		}
		gameObject.rigidbody.velocity = newVelocity;
	}

	Vector3 CheckPosition(Vector3 position){
		CreateEnvironment createEnvironment = Camera.main.GetComponent<CreateEnvironment> ();
		Vector3 bottomPlanePos = createEnvironment.bottomPlane.transform.position;
		if (position.y > bottomPlanePos.y + (createEnvironment.height) - createEnvironment.errorBuffer) {
			position.y = bottomPlanePos.y + (createEnvironment.height) - createEnvironment.errorBuffer;
		}
		if (position.y < bottomPlanePos.y + createEnvironment.errorBuffer) {
			position.y = bottomPlanePos.y + createEnvironment.errorBuffer;
		}
		if (position.x > bottomPlanePos.x + (createEnvironment.width/2.0f) - createEnvironment.errorBuffer) {
			position.x = bottomPlanePos.x + (createEnvironment.width/2.0f) - createEnvironment.errorBuffer;
		}
		if (position.x < bottomPlanePos.x - (createEnvironment.width/2.0f) + createEnvironment.errorBuffer) {
			position.x = bottomPlanePos.x - (createEnvironment.width/2.0f) + createEnvironment.errorBuffer;
		}
		if (position.z > bottomPlanePos.z + (createEnvironment.depth/2.0f) - createEnvironment.errorBuffer) {
			position.z = bottomPlanePos.z + (createEnvironment.depth/2.0f) - createEnvironment.errorBuffer;
		}
		if (position.z < bottomPlanePos.z - (createEnvironment.depth/2.0f) + createEnvironment.errorBuffer) {
			position.z = bottomPlanePos.z - (createEnvironment.depth/2.0f) + createEnvironment.errorBuffer;
		}
		return position;
	}

	void ApplyTransparency(){
		GameObject[] allMolecules = GameObject.FindGameObjectsWithTag("Molecule");
		for (int i = 0; i < allMolecules.Length; i++) {
			GameObject neighbor = allMolecules[i];
			if(neighbor == gameObject) continue;
			Atom neighborScript = neighbor.GetComponent<Atom>();
			if(neighborScript.selected){
				neighborScript.SetSelected(neighborScript.selected);
			}
			else if(!neighborScript.selected && Vector3.Distance(gameObject.transform.position, neighbor.transform.position) > BondDistance(neighbor)){
				neighborScript.SetTransparent(true);
			}
			else{
				neighborScript.SetTransparent(false);
			}
		}
	}

	public void ResetTransparency(){
		GameObject[] allMolecules = GameObject.FindGameObjectsWithTag("Molecule");
		for (int i = 0; i < allMolecules.Length; i++) {
			GameObject currAtom = allMolecules[i];
			Atom atomScript = currAtom.GetComponent<Atom>();
			if(atomScript.selected){
				atomScript.SetSelected(atomScript.selected);
			}
			else{
				atomScript.SetTransparent(false);
			}
		}
	}

	void UpdateBondText(){
		Quaternion cameraRotation = Camera.main.transform.rotation;
		Vector3 left = cameraRotation * -Vector3.right;
		Vector3 right = cameraRotation * Vector3.right;

		GameObject[] allMolecules = GameObject.FindGameObjectsWithTag("Molecule");
		for (int i = 0; i < allMolecules.Length; i++) {
			GameObject atomNeighbor = allMolecules[i];
			if(atomNeighbor == gameObject) continue;
			float distance = Vector3.Distance(gameObject.transform.position, atomNeighbor.transform.position);
			if(distance < BondDistance(atomNeighbor)){

				TextMesh bondDistance = null;

				Vector3 midpoint = new Vector3((gameObject.transform.position.x + atomNeighbor.transform.position.x) / 2.0f, (gameObject.transform.position.y + atomNeighbor.transform.position.y) / 2.0f, (gameObject.transform.position.z + atomNeighbor.transform.position.z) / 2.0f);
				
				if(atomNeighbor.transform.position.x > gameObject.transform.position.x){
					Vector3 direction = gameObject.transform.position - atomNeighbor.transform.position;
					float angle = Vector3.Angle(direction, right);
					float percentToChange = (angle - 90) / 90.0f;
					midpoint += (direction * (.15f * percentToChange));
				}
				else{
					Vector3 direction = atomNeighbor.transform.position - gameObject.transform.position;
					float angle = Vector3.Angle(direction, right);
					float percentToChange = (angle - 90) / 90.0f;
					midpoint += (direction * (.15f * percentToChange));
				}

				try{
					bondDistance = bondDistanceText[atomNeighbor.name];
					bondDistance.transform.rotation = cameraRotation;
					bondDistance.transform.position = midpoint;
				}catch (KeyNotFoundException e){
					bondDistance = Instantiate(textMeshPrefab, midpoint, cameraRotation) as TextMesh;
					bondDistanceText.Add(atomNeighbor.name, bondDistance);
				}
				bondDistance.text = (Math.Round(distance, 1)).ToString();
			}
			else{
				//we need to check if there is text and if there is, remove it. Otherwise dont do anything
				try{
					TextMesh bondDistance = bondDistanceText[atomNeighbor.name];
					Destroy(bondDistance);
					bondDistanceText.Remove(atomNeighbor.name);
				}catch(KeyNotFoundException e){} //dont do anything with the caught exception
			}
		}

	}
	
	public void RemoveBondText(){
		foreach (KeyValuePair<String, TextMesh> keyValue in bondDistanceText) {
			TextMesh bondDistance = keyValue.Value;
			Destroy(bondDistance);
		}
		bondDistanceText.Clear ();
	}

	void RemoveAllBondText(){
		GameObject[] allMolecules = GameObject.FindGameObjectsWithTag("Molecule");
		for (int i = 0; i < allMolecules.Length; i++) {
			GameObject currAtom = allMolecules[i];
			Atom atomScript = currAtom.GetComponent<Atom>();
			atomScript.RemoveBondText();
		}
	}


}

