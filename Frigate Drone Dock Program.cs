//Docking program for a specific dock structure

public enum DockingStatus {
	Dysfunctional = 0,
	Undocked = 1,
	Undocking = 2,
	Docking = 3,
	Docked = 4
}


private DockingStatus DockStatus = DockingStatus.Undocked;
private DockingStatus LastStatus = DockingStatus.Undocked;
private List<IMyInteriorLight[]> DockLights = new List<IMyInteriorLight[]>();
private List<IMyAirtightHangarDoor> DockDoors = new List<IMyAirtightHangarDoor>();
private IMyShipConnector DockingConnector = null;
private IMyCargoContainer DockingCargo = null;
private IMyMotorAdvancedStator DockingRotor = null;
private IMySensorBlock DockingSensor = null;
private IMyBatteryBlock DockingBattery = null;
private Vector3D DockPosition1;
private Vector3D DockPosition2;
private Vector3D DockPosition3;

public void UpdateStatus(DockingStatus new_status){
	bool did_change = false;
	if(new_status != DockStatus){
		Echo("Changed status to " + new_status.ToString() + " (was " + DockStatus.ToString() + ")\n");
		LastStatus = DockStatus;
		DockStatus = new_status;
		did_change = true;
	}
	Color new_color;
	switch(new_status){
		case DockingStatus.Dysfunctional:
			new_color = new Color(255, 0, 0, 255);
			for(int i=0; i<DockLights.Count; i++){
				for(int j=0; j<2; j++){
					DockLights[i][j].Color=new_color;
					DockLights[i][j].BlinkLength=(float) 100.0;
					DockLights[i][j].BlinkOffset=0;
					DockLights[i][j].BlinkIntervalSeconds=0;
				}
			}
			foreach(IMyAirtightHangarDoor door in DockDoors){
				door.CloseDoor();
			}
			break;
		case DockingStatus.Undocked:
			new_color = new Color(255, 239, 137, 255);
			for(int i=0; i<DockLights.Count; i++){
				for(int j=0; j<2; j++){
					DockLights[i][j].Color=new_color;
					DockLights[i][j].BlinkLength=(float) 50.0;
					DockLights[i][j].BlinkOffset=0;
					DockLights[i][j].BlinkIntervalSeconds=2;
				}
			}
			foreach(IMyAirtightHangarDoor door in DockDoors){
				door.CloseDoor();
			}
			break;
		case DockingStatus.Undocking:
			new_color = new Color(255, 239, 137, 255);
			for(int i=0; i<DockLights.Count; i++){
				float blink_offset = ((float) ((DockLights.Count - i) * 100.0)) / DockLights.Count;
				for(int j=0; j<2; j++){
					DockLights[i][j].Color=new_color;
					DockLights[i][j].BlinkLength=(float) (100.0 / 3.0);
					DockLights[i][j].BlinkOffset=blink_offset;
					DockLights[i][j].BlinkIntervalSeconds=2;
				}
			}
			foreach(IMyAirtightHangarDoor door in DockDoors){
				door.OpenDoor();
			}
			break;
		case DockingStatus.Docking:
			new_color = new Color(151, 239, 255, 255);
			for(int i=0; i<DockLights.Count; i++){
				float blink_offset = ((float) (i* 100.0)) / DockLights.Count;
				for(int j=0; j<2; j++){
					DockLights[i][j].Color=new_color;
					DockLights[i][j].BlinkLength=(float) (100.0 / 3.0);
					DockLights[i][j].BlinkOffset=blink_offset;
					DockLights[i][j].BlinkIntervalSeconds=2;
				}
			}
			foreach(IMyAirtightHangarDoor door in DockDoors){
				door.OpenDoor();
			}
			break;
		case DockingStatus.Docked:
			new_color = new Color(151, 239, 255, 255);
			for(int i=0; i<DockLights.Count; i++){
				for(int j=0; j<2; j++){
					DockLights[i][j].Color=new_color;
					DockLights[i][j].BlinkLength=(float) 50.0;
					DockLights[i][j].BlinkOffset=0;
					DockLights[i][j].BlinkIntervalSeconds=4;
				}
			}
			foreach(IMyAirtightHangarDoor door in DockDoors){
				door.CloseDoor();
			}
			if(did_change && LastStatus == DockingStatus.Docking){
				if(DockingRotor.TargetVelocityRPM >= 0)
					DockingRotor.TargetVelocityRPM = (float) -6.0;
				else
					DockingRotor.TargetVelocityRPM = (float) 6.0;
			}
			break;
	}
	Me.GetSurface(0).WriteText("Dock Status\n" + DockStatus.ToString(), false);
}

private void SetLights(){
	DockLights = new List<IMyInteriorLight[]>();
	List<IMyInteriorLight> AllLights = new List<IMyInteriorLight>();
	GridTerminalSystem.GetBlocksOfType<IMyInteriorLight>(AllLights);
	List<IMyInteriorLight> AllDockLights = new List<IMyInteriorLight>();
	foreach(IMyInteriorLight light in AllLights){
		if(light.CustomName.ToLower().Contains("dock light ")){
			AllDockLights.Add(light);
		}
	}
	List<List<IMyInteriorLight>> SomeDockLights = new List<List<IMyInteriorLight>>();
	foreach(IMyInteriorLight docklight in AllDockLights){
		string tag = docklight.CustomName.Substring(docklight.CustomName.ToLower().IndexOf("dock light ") + "dock light ".Length, 2).ToUpper();
		docklight.CustomName = "Dock Light " + tag;
		bool new_name = true;
		foreach(List<IMyInteriorLight> list in SomeDockLights){
			if(list[0].CustomName.Equals(docklight.CustomName)){
				new_name = false;
				list.Add(docklight);
				break;
			}
		}
		if(new_name){
			List<IMyInteriorLight> new_list = new List<IMyInteriorLight>();
			new_list.Add(docklight);
			SomeDockLights.Add(new_list);
		}
	}
	char[] chars = new char[] {'A','B','C','D','E','F','G','H','I'};
	char[] nums = new char[] {'1','2'};
	foreach(char ch in chars){
		IMyInteriorLight light1 = null;
		IMyInteriorLight light2 = null;
		foreach(char num in nums){
			foreach(List<IMyInteriorLight> list in SomeDockLights){
				if(list[0].CustomName.Equals("Dock Light " + ch + num)){
					if(list.Count == 1){
						if(num == '1')
							light1 = list[0];
						else
							light2 = list[0];
					}
					else if(list.Count > 1){
						double min_distance = double.MaxValue;
						foreach(IMyInteriorLight light in list){
							double distance = (DockingConnector.GetPosition() - light.GetPosition()).Length();
							min_distance = Math.Min(min_distance, distance);
						}
						foreach(IMyInteriorLight light in list){
							double distance = (DockingConnector.GetPosition() - light.GetPosition()).Length();
							if(distance <= min_distance + 0.1){
								if(num=='1')
									light1 = light;
								else
									light2 = light;
								break;
							}
						}
					}
					break;
				}
			}
		}
		if(light1 != null && light2 != null){
			IMyInteriorLight[] arr = new IMyInteriorLight[] {light1, light2};
			DockLights.Add(arr);
		}
		else{
			if(light1 == null){
				Echo("Missing \"Docking Light " + ch + "1\"\n");
			}
			if(light2 == null){
				Echo("Missing \"Docking Light " + ch + "2\"\n");
			}
		}
	}
	Echo("Set " + (2 * DockLights.Count).ToString() + " / " + AllLights.Count.ToString() + " lights\n");
}

private void SetDoors(){
	DockDoors = new List<IMyAirtightHangarDoor>();
	List<IMyAirtightHangarDoor> AllHangars = new List<IMyAirtightHangarDoor>();
	GridTerminalSystem.GetBlocksOfType<IMyAirtightHangarDoor>(AllHangars);
	List<IMyAirtightHangarDoor> AllDockDoors = new List<IMyAirtightHangarDoor>();
	foreach(IMyAirtightHangarDoor door in AllHangars){
		if(door.CustomName.ToLower().Contains("dock door ")){
			AllDockDoors.Add(door);
		}
	}
	List<List<IMyAirtightHangarDoor>> SomeDockDoors = new List<List<IMyAirtightHangarDoor>>();
	foreach(IMyAirtightHangarDoor dockdoor in AllDockDoors){
		string tag = dockdoor.CustomName.Substring(dockdoor.CustomName.ToLower().IndexOf("dock door ") + "dock door ".Length).ToUpper();
		tag = tag.Substring(0, Math.Min(tag.Length, 2));
		dockdoor.CustomName = "Dock Door " + tag;
		bool new_name = true;
		foreach(List<IMyAirtightHangarDoor> list in SomeDockDoors){
			if(list[0].CustomName.Equals(dockdoor.CustomName)){
				new_name = false;
				list.Add(dockdoor);
				break;
			}
		}
		if(new_name){
			List<IMyAirtightHangarDoor> new_list = new List<IMyAirtightHangarDoor>();
			new_list.Add(dockdoor);
			SomeDockDoors.Add(new_list);
		}
	}
	
	foreach(List<IMyAirtightHangarDoor> list in SomeDockDoors){
		if(list.Count == 1){
			DockDoors.Add(list[0]);
		}
		else if(list.Count > 1){
			double min_distance = double.MaxValue;
			foreach(IMyAirtightHangarDoor door in list){
				double distance = (DockingConnector.GetPosition() - door.GetPosition()).Length();
				min_distance = Math.Min(min_distance, distance);
			}
			foreach(IMyAirtightHangarDoor door in list){
				double distance = (DockingConnector.GetPosition() - door.GetPosition()).Length();
				if(distance <= min_distance + 0.1){
					DockDoors.Add(door);
					break;
				}
			}
		}
	}
	Echo("Set " + DockDoors.Count.ToString() + " / " + AllHangars.Count.ToString() + " doors\n");
}

//Sets the blocks as necessary. 
public bool SetBlocks(){
	DockingConnector = null;
	List<IMyShipConnector> AllConnectors = new List<IMyShipConnector>();
	GridTerminalSystem.GetBlocksOfType<IMyShipConnector>(AllConnectors);
	List<IMyShipConnector> DockConnectors = new List<IMyShipConnector>();
	double min_distance = double.MaxValue;
	foreach(IMyShipConnector connector in AllConnectors){
		if(connector.CustomName.ToLower().Contains("Dock Connector".ToLower())){
			double distance = (Me.GetPosition() - connector.GetPosition()).Length();
			DockConnectors.Add(connector);
			min_distance = Math.Min(min_distance, distance);
		}
	}
	foreach(IMyShipConnector connector in DockConnectors){
		double distance = (Me.GetPosition() - connector.GetPosition()).Length();
		if(distance <= min_distance + 0.1){
			DockingConnector = connector;
			DockingConnector.CustomName = "Dock Connector";
			break;
		}
	}
	if(DockingConnector==null){
		Echo("Could not find a valid Docking Connector (" + AllConnectors.Count + " total Connectors)\n");
		return false;
	}
	
	DockingCargo = null;
	List<IMyCargoContainer> AllCargo = new List<IMyCargoContainer>();
	GridTerminalSystem.GetBlocksOfType<IMyCargoContainer>(AllCargo);
	List<IMyCargoContainer> DockCargo = new List<IMyCargoContainer>();
	min_distance = double.MaxValue;
	foreach(IMyCargoContainer cargo in AllCargo){
		if(cargo.CustomName.ToLower().Contains("dock")){
			double distance = (DockingConnector.GetPosition() - cargo.GetPosition()).Length();
			DockCargo.Add(cargo);
			min_distance = Math.Min(min_distance, distance);
		}
	}
	foreach(IMyCargoContainer cargo in DockCargo){
		double distance = (DockingConnector.GetPosition() - cargo.GetPosition()).Length();
		if(distance <= min_distance + 0.1){
			DockingCargo = cargo;
			DockingCargo.CustomName = "Dock Cargo Container";
			break;
		}
	}
	if(DockingCargo==null){
		Echo("Could not find a valid Docking Cargo Container\n");
		return false;
	}
	
	DockingRotor = null;
	List<IMyMotorAdvancedStator> AllRotors = new List<IMyMotorAdvancedStator>();
	GridTerminalSystem.GetBlocksOfType<IMyMotorAdvancedStator>(AllRotors);
	List<IMyMotorAdvancedStator> DockRotors = new List<IMyMotorAdvancedStator>();
	min_distance = double.MaxValue;
	foreach(IMyMotorAdvancedStator rotor in AllRotors){
		if(rotor.CustomName.ToLower().Contains("dock")){
			double distance = (DockingConnector.GetPosition() - rotor.GetPosition()).Length();
			DockRotors.Add(rotor);
			min_distance = Math.Min(min_distance, distance);
		}
	}
	foreach(IMyMotorAdvancedStator rotor in DockRotors){
		double distance = (DockingConnector.GetPosition() - rotor.GetPosition()).Length();
		if(distance <= min_distance + 0.1){
			DockingRotor = rotor;
			DockingRotor.CustomName = "Dock Rotor";
			break;
		}
	}
	if(DockingRotor==null){
		Echo("Could not find a valid Docking Rotor\n");
		return false;
	}
	
	DockingSensor = null;
	List<IMySensorBlock> AllSensors = new List<IMySensorBlock>();
	GridTerminalSystem.GetBlocksOfType<IMySensorBlock>(AllSensors);
	List<IMySensorBlock> DockSensors = new List<IMySensorBlock>();
	min_distance = double.MaxValue;
	foreach(IMySensorBlock sensor in AllSensors){
		if(sensor.CustomName.ToLower().Contains("dock")){
			double distance = (DockingConnector.GetPosition() - sensor.GetPosition()).Length();
			DockSensors.Add(sensor);
			min_distance = Math.Min(min_distance, distance);
		}
	}
	foreach(IMySensorBlock sensor in DockSensors){
		double distance = (DockingConnector.GetPosition() - sensor.GetPosition()).Length();
		if(distance <= min_distance + 0.1){
			DockingSensor = sensor;
			DockingSensor.CustomName = "Dock Sensor";
			break;
		}
	}
	if(DockingSensor==null){
		Echo("Could not find a valid Docking Sensor\n");
		return false;
	}
	
	DockingBattery = null;
	List<IMyBatteryBlock> AllBatteries = new List<IMyBatteryBlock>();
	GridTerminalSystem.GetBlocksOfType<IMyBatteryBlock>(AllBatteries);
	List<IMyBatteryBlock> DockBatteries = new List<IMyBatteryBlock>();
	min_distance = double.MaxValue;
	foreach(IMyBatteryBlock battery in AllBatteries){
		if(battery.CustomName.ToLower().Contains("dock")){
			double distance = (DockingConnector.GetPosition() - battery.GetPosition()).Length();
			DockBatteries.Add(battery);
			min_distance = Math.Min(min_distance, distance);
		}
	}
	foreach(IMyBatteryBlock battery in DockBatteries){
		double distance = (DockingConnector.GetPosition() - battery.GetPosition()).Length();
		if(distance <= min_distance + 0.1){
			DockingBattery = battery;
			DockingBattery.CustomName = "Dock Battery";
			break;
		}
	}
	if(DockingBattery==null){
		Echo("Could not find a valid Docking Battery\n");
		return false;
	}
	
	SetLights();
	SetDoors();
	
	return true;
}

private bool HasPossibleDockingEntity(){
	List<MyDetectedEntityInfo> entities = new List<MyDetectedEntityInfo>();
	DockingSensor.DetectedEntities(entities);
	foreach(MyDetectedEntityInfo entity in entities){
		if(entity.Type == MyDetectedEntityType.SmallGrid){
			if(entity.Relationship == MyRelationsBetweenPlayerAndBlock.Owner || entity.Relationship == MyRelationsBetweenPlayerAndBlock.FactionShare || entity.Relationship == MyRelationsBetweenPlayerAndBlock.Friends){
				return true;
			}
		}
	}
	return false;
}

public bool HasDockingEntity(){
	List<MyDetectedEntityInfo> entities = new List<MyDetectedEntityInfo>();
	DockingSensor.DetectedEntities(entities);
	foreach(MyDetectedEntityInfo entity in entities){
		if(entity.Type == MyDetectedEntityType.SmallGrid){
			if(entity.Relationship == MyRelationsBetweenPlayerAndBlock.Owner || entity.Relationship == MyRelationsBetweenPlayerAndBlock.FactionShare || entity.Relationship == MyRelationsBetweenPlayerAndBlock.Friends){
				if(entity.Velocity.Length() < 10){
					return true;
				}
			}
		}
	}
	return false;
}

public DockingStatus GetStatus(){
	if(DockingConnector.Status == MyShipConnectorStatus.Connected)
		return DockingStatus.Docked;
	if(HasDockingEntity()){
		if(LastStatus == DockingStatus.Docked || DockStatus == DockingStatus.Docked)
			return DockingStatus.Undocking;
		if(DockStatus == DockingStatus.Undocking)
			return DockingStatus.Undocking;
		//if(LastStatus == DockingStatus.Undocked || DockStatus == DockingStatus.Undocked)
			//return DockingStatus.Docking;
		//if(DockStatus == DockingStatus.Docking)
			//return DockingStatus.Docking;
		return DockingStatus.Docking;
	}
	else{
		if(DockStatus == DockingStatus.Dysfunctional)
			return DockStatus;
		return DockingStatus.Undocked;
	}
}

public void UpdateCustomData(){
	Vector3D down_vector = DockingRotor.GetPosition() - DockingCargo.GetPosition();
	down_vector.Normalize();
	Vector3D out_vector = DockingBattery.GetPosition() - DockingCargo.GetPosition();
	out_vector.Normalize();
	DockPosition3 = DockingConnector.GetPosition() + (5.5 * down_vector) - (out_vector);
	DockPosition2 = DockPosition2 + (40 * out_vector);
	DockPosition1 = DockPosition2 + (10 * down_vector);
	Me.CustomData = DockStatus.ToString() + "\n" + DockPosition1.ToString() + "\n" + DockPosition2.ToString() + "\n" + DockPosition3.ToString();
}

public Program()
{
	if(SetBlocks()){
		Echo("Successfully set blocks\n");
		Runtime.UpdateFrequency = UpdateFrequency.Update100;
		UpdateCustomData();
		try{
			string line = this.Storage;
			if(this.Storage.Contains('\n'))
				line = line.Substring(0,line.IndexOf('\n'));
			DockingStatus new_status = (DockingStatus) Int32.Parse(line);
			if(this.Storage.Contains('\n')){
				line = this.Storage.Substring(this.Storage.IndexOf('\n')+1);
				LastStatus = (DockingStatus) Int32.Parse(line);
				DockStatus = LastStatus;
			}
			if(new_status != DockingStatus.Dysfunctional)
				UpdateStatus(new_status);
			else{
				UpdateStatus(GetStatus());
			}
		} catch(FormatException){
			UpdateStatus(GetStatus());
		}
		this.Storage = "";
	}
	else{
		Echo("Failed to set blocks\n");
		Runtime.UpdateFrequency = UpdateFrequency.None;
		try{
			UpdateStatus(DockingStatus.Dysfunctional);
		}
		catch(Exception){
			DockStatus = DockingStatus.Dysfunctional;
			Me.GetSurface(0).WriteText("Dock Status\n" + DockStatus.ToString(), false);
		}
	}
	Echo("DockStatus: " + DockStatus.ToString() + "\n");
	Echo("LastStatus: " + LastStatus.ToString() + "\n");
}

public void Save()
{
	this.Storage = ((int)DockStatus).ToString() + '\n' + ((int)LastStatus).ToString();
}

public void Main(string argument, UpdateType updateSource)
{
	UpdateStatus(GetStatus());
	UpdateCustomData();
	Echo("DockStatus: " + DockStatus.ToString() + "\n");
	Echo("LastStatus: " + LastStatus.ToString() + "\n");
}
