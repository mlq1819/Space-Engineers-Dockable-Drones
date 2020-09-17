

public enum DroneTask{
	Error = -1,
	Fresh = 0,
	Idle = 1,
	Docked = 2,
	Waiting = 3,
	Aligning = 4,
	Docking = 5,
	Undocking = 6,
	Returning = 7,
	Swarming = 8
}

private string DroneIdentification = "";

private IMyRemoteControl DroneRemote = null;
private IMyRadioAntenna DroneAntenna = null;
private IMyShipConnector DroneConnector = null;
private IMySensorBlock DroneSensor = null;
private IMyTimerBlock DroneTimer = null;
private List<IMyReflectorLight> DroneSpotlights = new List<IMyReflectorLight>();

public DroneTask CurrentTask = DroneTask.Fresh;
public DroneTask LastTask = DroneTask.Fresh;
public Queue<DroneTask> TaskQueue = new Queue<DroneTask>();
public Vector3D DockConnector = new Vector3D(0,0,0);
public Vector3D DockAlign = new Vector3D(0,0,0);
public Vector3D DockApproach = new Vector3D(0,0,0);

private List<string> listener_tags = new List<string>();

private Vector3D follow_position = new Vector3D(0,0,0);
private Vector3D follow_velocity = new Vector3D(0,0,0);
private Vector3D target_position = new Vector3D(0,0,0);
private double follow_speed;
private bool following = false;
private bool waiting = false;

private long Cycle = 0;
private long Cycle_Long = 1;
private char loadingChar = '|';

public void NextTask(){
	if(TaskQueue.Count > 0){
		DroneTask new_task = TaskQueue.Dequeue();
		UpdateTask(new_task);
	}
	else{
		UpdateTask(DroneTask.Idle);
	}
}

public void AddTask(DroneTask new_task){
	if(((int)new_task)>((int)DroneTask.Docked)){
		TaskQueue.Enqueue(new_task);
	}
	if(CurrentTask == DroneTask.Idle)
		NextTask();
}

public void PushTask(DroneTask new_task){
	Queue<DroneTask> new_queue = new Queue<DroneTask>();
	new_queue.Enqueue(new_task);
	while(TaskQueue.Count > 0){
		new_queue.Enqueue(TaskQueue.Dequeue());
	}
	TaskQueue = new_queue;
}

public void UpdateTask(DroneTask new_task){
	if(new_task != CurrentTask){
		LastTask = CurrentTask;
		CurrentTask = new_task;
		if(LastTask == DroneTask.Docked && CurrentTask != DroneTask.Waiting){
			PushTask(CurrentTask);
			PushTask(DroneTask.Undocking);
			CurrentTask = DroneTask.Waiting;
			Echo("Delaying task to Undock\n");
		}
		if(CurrentTask == DroneTask.Docking && LastTask != DroneTask.Aligning){
			PushTask(DroneTask.Docking);
			PushTask(DroneTask.Aligning);
			CurrentTask = DroneTask.Returning;
			Echo("Failed to align before docking; returning to align...\n");
		}
		switch(LastTask){
			case DroneTask.Error:
				Runtime.UpdateFrequency = UpdateFrequency.Update100;
				break;
			case DroneTask.Fresh:
				break;
			case DroneTask.Idle:
				break;
			case DroneTask.Docked:
				foreach(IMyReflectorLight spotlight in DroneSpotlights){
					spotlight.ApplyAction("OnOff_On");
				}
				DroneAntenna.Radius = (float) Math.Max(5000, 2 * (DroneRemote.GetPosition() - follow_position).Length());
				break;
			case DroneTask.Waiting:
				DroneRemote.ClearWaypoints();
				waiting = false;
				break;
			case DroneTask.Aligning:
				DroneRemote.ClearWaypoints();
				DroneRemote.SetAutoPilotEnabled(false);
				break;
			case DroneTask.Docking:
				DroneRemote.ClearWaypoints();
				DroneRemote.SetAutoPilotEnabled(false);
				break;
			case DroneTask.Undocking:
				DroneRemote.ClearWaypoints();
				DroneRemote.SetAutoPilotEnabled(false);
				break;
			case DroneTask.Returning:
				DroneRemote.ClearWaypoints();
				DroneRemote.SetAutoPilotEnabled(false);
				break;
			case DroneTask.Swarming:
				DroneTimer.StopCountdown();
				following = false;
				evasion = false;
				Evasion();
				DroneRemote.ClearWaypoints();
				break;
		}
		switch(CurrentTask){
			case DroneTask.Error:
				Runtime.UpdateFrequency = UpdateFrequency.None;
				break;
			case DroneTask.Fresh:
				break;
			case DroneTask.Idle:
				break;
			case DroneTask.Docked:
				foreach(IMyReflectorLight spotlight in DroneSpotlights){
					spotlight.ApplyAction("OnOff_Off");
				}
				DroneAntenna.Radius = 600;
				break;
			case DroneTask.Waiting:
				waiting = true;
				DroneTimer.StopCountdown();
				DroneTimer.StartCountdown();
				break;
			case DroneTask.Aligning:
				DroneRemote.ApplyAction("Up");
				DroneRemote.AddWaypoint(DockConnector, "Align");
				DroneRemote.SpeedLimit = 10;
				DroneRemote.SetCollisionAvoidance(true);
				DroneRemote.SetDockingMode(true);
				DroneRemote.SetAutoPilotEnabled(true);
				break;
			case DroneTask.Docking:
				DroneRemote.ApplyAction("Forward");
				DroneRemote.AddWaypoint(DockConnector, "Dock");
				DroneRemote.SpeedLimit = 10;
				DroneRemote.SetCollisionAvoidance(true);
				DroneRemote.SetDockingMode(true);
				DroneRemote.SetAutoPilotEnabled(true);
				break;
			case DroneTask.Undocking:
				DroneRemote.DampenersOverride = true;
				if(DroneRemote.GetShipSpeed()>0.01){
					Echo("Waiting for slowdown\n");
				}
				else{
					DroneConnector.Disconnect();
					Vector3D forward = (Me.GetPosition() - DroneRemote.GetPosition());
					forward.Normalize();
					forward = (forward * 40) + DroneRemote.GetPosition();
					DroneRemote.ApplyAction("Forward");
					DroneRemote.AddWaypoint(forward, "Undocking");
					DroneRemote.SpeedLimit = 10;
					DroneRemote.SetCollisionAvoidance(true);
					DroneRemote.SetDockingMode(true);
					DroneRemote.SetAutoPilotEnabled(true);
				}
				break;
			case DroneTask.Returning:
				DroneRemote.ApplyAction("Forward");
				DroneRemote.AddWaypoint(DockApproach, "Returning");
				DroneRemote.SpeedLimit = 80;
				DroneRemote.SetCollisionAvoidance(true);
				DroneRemote.SetDockingMode(false);
				DroneRemote.SetAutoPilotEnabled(true);
				break;
			case DroneTask.Swarming:
				DroneTimer.StartCountdown();
				following = true;
				evasion = true;
				break;
		}
	}
}

public void PerformCurrentTask(){
	Vector3D target;
	switch(CurrentTask){
			case DroneTask.Error:
				break;
			case DroneTask.Fresh:
				break;
			case DroneTask.Idle:
				break;
			case DroneTask.Docked:
				break;
			case DroneTask.Waiting:
				break;
			case DroneTask.Aligning:
				target = DroneRemote.CurrentWaypoint.Coords;
				if((target - DroneRemote.GetPosition()).Length() < 5 && DroneRemote.GetShipSpeed() < 5){
					NextTask();
				}
				break;
			case DroneTask.Docking:
				if(DroneConnector.Status != MyShipConnectorStatus.Unconnected){
					DroneConnector.Connect();
					if(DroneConnector.Status == MyShipConnectorStatus.Connected){
						UpdateTask(DroneTask.Docked);
					}
				}
				break;
			case DroneTask.Undocking:
				if(DroneRemote.IsAutoPilotEnabled){
					target = DroneRemote.CurrentWaypoint.Coords;
					if((target - DroneRemote.GetPosition()).Length() < 5 && DroneRemote.GetShipSpeed() < 5){
						NextTask();
					}
				}
				else{
					DroneRemote.DampenersOverride = true;
					if(DroneRemote.GetShipSpeed()>0.01){
						Echo("Waiting for slowdown\n");
					}
					else{
						DroneConnector.Disconnect();
						Vector3D forward = (Me.GetPosition() - DroneRemote.GetPosition());
						forward.Normalize();
						forward = (forward * 40) + DroneRemote.GetPosition();
						DroneRemote.ApplyAction("Forward");
						DroneRemote.AddWaypoint(forward, "Undocking");
						DroneRemote.SpeedLimit = 10;
						DroneRemote.SetCollisionAvoidance(true);
						DroneRemote.SetDockingMode(true);
						DroneRemote.SetAutoPilotEnabled(true);
					}
				}
				break;
			case DroneTask.Returning:
				target = DroneRemote.CurrentWaypoint.Coords;
				if((target - DroneRemote.GetPosition()).Length() < 5 && DroneRemote.GetShipSpeed() < 5){
					NextTask();
				}
				break;
			case DroneTask.Swarming:
				UpdateNavigation();
				break;
		}
}

public bool SetBlocks(){
	DroneRemote = null;
	List<IMyRemoteControl> AllRemotes = new List<IMyRemoteControl>();
	GridTerminalSystem.GetBlocksOfType<IMyRemoteControl>(AllRemotes);
	List<IMyRemoteControl> DroneRemotes = new List<IMyRemoteControl>();
	double min_distance = double.MaxValue;
	foreach(IMyRemoteControl remote in AllRemotes){
		if(remote.CustomName.ToLower().Contains("Drone Remote Control".ToLower())){
			double distance = (Me.GetPosition() - remote.GetPosition()).Length();
			DroneRemotes.Add(remote);
			min_distance = Math.Min(min_distance, distance);
		}
	}
	foreach(IMyRemoteControl remote in DroneRemotes){
		double distance = (Me.GetPosition() - remote.GetPosition()).Length();
		if(distance <= min_distance + 0.1){
			DroneRemote = remote;
			DroneRemote.CustomName = "Drone Remote Control";
			break;
		}
	}
	if(DroneRemote==null){
		Echo("Could not find a valid Drone Remote Control (" + AllRemotes.Count + " total Remotes)\n");
		return false;
	}
	
	DroneAntenna = null;
	List<IMyRadioAntenna> AllAntenna = new List<IMyRadioAntenna>();
	GridTerminalSystem.GetBlocksOfType<IMyRadioAntenna>(AllAntenna);
	List<IMyRadioAntenna> DroneAntennae = new List<IMyRadioAntenna>();
	min_distance = double.MaxValue;
	foreach(IMyRadioAntenna antenna in AllAntenna){
		if(antenna.CustomName.ToLower().Contains("Drone Antenna".ToLower())){
			double distance = (Me.GetPosition() - antenna.GetPosition()).Length();
			DroneAntennae.Add(antenna);
			min_distance = Math.Min(min_distance, distance);
		}
	}
	foreach(IMyRadioAntenna antenna in DroneAntennae){
		double distance = (Me.GetPosition() - antenna.GetPosition()).Length();
		if(distance <= min_distance + 0.1){
			DroneAntenna = antenna;
			DroneAntenna.CustomName = "Drone Antenna";
			break;
		}
	}
	if(DroneAntenna==null){
		Echo("Could not find a valid Drone Antenna (" + AllAntenna.Count + " total Antennae)\n");
		return false;
	}
	
	DroneConnector = null;
	List<IMyShipConnector> AllConnectors = new List<IMyShipConnector>();
	GridTerminalSystem.GetBlocksOfType<IMyShipConnector>(AllConnectors);
	List<IMyShipConnector> DroneConnectors = new List<IMyShipConnector>();
	min_distance = double.MaxValue;
	foreach(IMyShipConnector connector in AllConnectors){
		if(connector.CustomName.ToLower().Contains("Drone Connector".ToLower())){
			double distance = (Me.GetPosition() - connector.GetPosition()).Length();
			DroneConnectors.Add(connector);
			min_distance = Math.Min(min_distance, distance);
		}
	}
	foreach(IMyShipConnector connector in DroneConnectors){
		double distance = (Me.GetPosition() - connector.GetPosition()).Length();
		if(distance <= min_distance + 0.1){
			DroneConnector = connector;
			DroneConnector.CustomName = "Drone Connector";
			break;
		}
	}
	if(DroneConnector==null){
		Echo("Could not find a valid Drone Connector (" + AllConnectors.Count + " total Connectors)\n");
		return false;
	}
	
	DroneSensor = null;
	List<IMySensorBlock> AllSensors = new List<IMySensorBlock>();
	GridTerminalSystem.GetBlocksOfType<IMySensorBlock>(AllSensors);
	List<IMySensorBlock> DroneSensors = new List<IMySensorBlock>();
	min_distance = double.MaxValue;
	foreach(IMySensorBlock sensor in AllSensors){
		if(sensor.CustomName.ToLower().Contains("Drone Sensor".ToLower())){
			double distance = (Me.GetPosition() - sensor.GetPosition()).Length();
			DroneSensors.Add(sensor);
			min_distance = Math.Min(min_distance, distance);
		}
	}
	foreach(IMySensorBlock sensor in DroneSensors){
		double distance = (Me.GetPosition() - sensor.GetPosition()).Length();
		if(distance <= min_distance + 0.1){
			DroneSensor = sensor;
			DroneSensor.CustomName = "Drone Sensor";
			break;
		}
	}
	if(DroneSensor==null){
		Echo("Could not find a valid Drone Sensor (" + AllSensors.Count + " total Sensors)\n");
		return false;
	}
	
	DroneTimer = null;
	List<IMyTimerBlock> AllTimers = new List<IMyTimerBlock>();
	GridTerminalSystem.GetBlocksOfType<IMyTimerBlock>(AllTimers);
	List<IMyTimerBlock> DroneTimers = new List<IMyTimerBlock>();
	min_distance = double.MaxValue;
	foreach(IMyTimerBlock timer in AllTimers){
		if(timer.CustomName.ToLower().Contains("Drone Timer".ToLower())){
			double distance = (Me.GetPosition() - timer.GetPosition()).Length();
			DroneTimers.Add(timer);
			min_distance = Math.Min(min_distance, distance);
		}
	}
	foreach(IMyTimerBlock timer in DroneTimers){
		double distance = (Me.GetPosition() - timer.GetPosition()).Length();
		if(distance <= min_distance + 0.1){
			DroneTimer = timer;
			DroneTimer.CustomName = "Drone Timer";
			break;
		}
	}
	if(DroneTimer==null){
		Echo("Could not find a valid Drone Timer (" + AllTimers.Count + " total Timers)\n");
		return false;
	}
	
	DroneSpotlights = new List<IMyReflectorLight>();
	List<IMyReflectorLight> AllSpotlights = new List<IMyReflectorLight>();
	GridTerminalSystem.GetBlocksOfType<IMyReflectorLight>(AllSpotlights);
	List<IMyReflectorLight> AllDroneSpotlights = new List<IMyReflectorLight>();
	foreach(IMyReflectorLight spotlight in AllSpotlights){
		if(spotlight.CustomName.ToLower().Contains("Drone Spotlight ".ToLower())){
			AllDroneSpotlights.Add(spotlight);
		}
	}
	List<List<IMyReflectorLight>> SomeDroneSpotlights = new List<List<IMyReflectorLight>>();
	foreach(IMyReflectorLight spotlight in AllDroneSpotlights){
		string tag = spotlight.CustomName.Substring(spotlight.CustomName.ToLower().IndexOf("Drone Spotlight ".ToLower()) + "Drone Spotlight ".Length, 1).ToUpper();
		spotlight.CustomName = "Drone Spotlight " + tag;
		bool new_name = true;
		foreach(List<IMyReflectorLight> list in SomeDroneSpotlights){
			if(list[0].CustomName.Equals(spotlight.CustomName)){
				new_name = false;
				list.Add(spotlight);
				break;
			}
		}
		if(new_name){
			List<IMyReflectorLight> new_list = new List<IMyReflectorLight>();
			new_list.Add(spotlight);
			SomeDroneSpotlights.Add(new_list);
		}
	}
	foreach(List<IMyReflectorLight> list in SomeDroneSpotlights){
		if(list.Count == 1){
			DroneSpotlights.Add(list[0]);
		}
		else if(list.Count > 1){
			min_distance = double.MaxValue;
			foreach(IMyReflectorLight spotlight in list){
				double distance = (Me.GetPosition() - spotlight.GetPosition()).Length();
				min_distance = Math.Min(min_distance, distance);
			}
			foreach(IMyReflectorLight spotlight in list){
				double distance = (Me.GetPosition() - spotlight.GetPosition()).Length();
				if(distance <= min_distance + 0.1){
					DroneSpotlights.Add(spotlight);
					break;
				}
			}
		}
	}
	
	Echo("All blocks properly initialized\n");
	return true;
}

public Program()
{
	if(SetBlocks()){
		Runtime.UpdateFrequency = UpdateFrequency.Update100;
		if(this.Storage.Length > 0){
			try{
				int index = 0;
				int length = this.Storage.IndexOf('\n');
				CurrentTask = (DroneTask) Int32.Parse(this.Storage.Substring(index, length).Trim());
				Echo("\tRestored CurrentTask: " + CurrentTask.ToString());
				
				index += length+1;
				length = this.Storage.Substring(index).IndexOf('\n');
				LastTask = (DroneTask) Int32.Parse(this.Storage.Substring(index, length).Trim());
				Echo("\tRestored LastTask: " + LastTask.ToString());
				
				index += length+1;
				length = this.Storage.Substring(index).IndexOf('\n');
				while(!this.Storage.Substring(index, length).ToLower().Equals("Vectors".ToLower())){
					TaskQueue.Enqueue((DroneTask) Int32.Parse(this.Storage.Substring(index, length).Trim()));
					Echo("\tRestored a Task");
					index += length+1;
					length = this.Storage.Substring(index).IndexOf('\n');
				}
				
				index += length+1;
				//Echo('\t' + this.Storage.Substring(index, this.Storage.Substring(index).IndexOf('\n')));
				index = this.Storage.Substring(index).IndexOf('(')+1;
				length = this.Storage.Substring(index).IndexOf(')');
				Vector3D.TryParse(this.Storage.Substring(index, length), out DockConnector);
				Echo("\tRestored DockConnector: (" + DockConnector.ToString() + ")");
				
				index += length+1;
				index = this.Storage.Substring(index).IndexOf('(')+1;
				length = this.Storage.Substring(index).IndexOf(')');
				Vector3D.TryParse(this.Storage.Substring(index, length), out DockAlign);
				Echo("\tRestored DockAlign: (" + DockAlign.ToString() + ")");
				
				index += length+1;
				index = this.Storage.Substring(index).IndexOf('(')+1;
				length = this.Storage.Substring(index).IndexOf(')');
				Vector3D.TryParse(this.Storage.Substring(index, length), out DockApproach);
				Echo("\tRestored DockApproach: (" + DockApproach.ToString() + ")");
				
				index += length+1;
				index = this.Storage.Substring(index).IndexOf('(')+1;
				length = this.Storage.Substring(index).IndexOf(')');
				Vector3D.TryParse(this.Storage.Substring(index, length), out follow_position);
				Echo("\tRestored follow_position: (" + follow_position.ToString() + ")");
				
				index += length+1;
				index = this.Storage.Substring(index).IndexOf('(')+1;
				length = this.Storage.Substring(index).IndexOf(')');
				Vector3D.TryParse(this.Storage.Substring(index, length), out follow_velocity);
				Echo("\tRestored follow_velocity: (" + follow_velocity.ToString() + ")");
				
				index += length+1;
				index = this.Storage.Substring(index).IndexOf('(')+1;
				length = this.Storage.Substring(index).IndexOf(')');
				Vector3D.TryParse(this.Storage.Substring(index, length), out target_position);
				Echo("\tRestored target_position: (" + target_position.ToString() + ")");
				
				DroneTask temp = CurrentTask;
				CurrentTask = LastTask;
				UpdateTask(temp);
			}
			catch(Exception e){
				Echo("Wiping storage\n");
				this.Storage = "";
			}
		}
		if(Me.CustomData.Length > 0 && CurrentTask != DroneTask.Fresh){
			DroneIdentification = Me.CustomData;
			Send(Me.CubeGrid.CustomName, "Started", DroneIdentification);
			
		}
		else {
			Random rnd = new Random();
			DroneIdentification = rnd.Next(1, Int32.MaxValue).ToString();
			Me.CustomData = DroneIdentification;
			DroneIdentification = Me.CubeGrid.CustomName + '-' + DroneIdentification;
			if(CurrentTask == DroneTask.Fresh)
				CurrentTask = DroneTask.Idle;
			Send(Me.CubeGrid.CustomName, "NewID", DroneIdentification);
		}
		listener_tags.Add(Me.CubeGrid.CustomName);
		listener_tags.Add(DroneIdentification);
	}
	else{
		UpdateTask(DroneTask.Error);
	}
}

public void Save()
{
	this.Storage = ((int)CurrentTask).ToString();
	this.Storage += '\n' + ((int)LastTask).ToString();
	while(TaskQueue.Count>0){
		this.Storage += '\n' + ((int)TaskQueue.Dequeue()).ToString();
	}
	this.Storage += "\nVectors";
	this.Storage += "\n(" + DockConnector.ToString() + ")";
	this.Storage += "\n(" + DockAlign.ToString() + ")";
	this.Storage += "\n(" + DockApproach.ToString() + ")";
	this.Storage += "\n(" + follow_position.ToString() + ")";
	this.Storage += "\n(" + follow_velocity.ToString() + ")";
	this.Storage += "\n(" + target_position.ToString() + ")";
}

private VRageMath.Base6Directions.Direction Opposite(VRageMath.Base6Directions.Direction direction){
	if(((int)direction)%2==0){
		return (VRageMath.Base6Directions.Direction)(((int)direction)+1);
	}
	else {
		return (VRageMath.Base6Directions.Direction)(((int)direction)-1);
	}
}

private bool evasion = false;
private void Evasion(){
	Random rnd = new Random();
	List<VRageMath.Base6Directions.Direction> RandomDirections = new List<VRageMath.Base6Directions.Direction>();
	List<IMyThrust> AllThrust = new List<IMyThrust>();
	GridTerminalSystem.GetBlocksOfType<IMyThrust>(AllThrust);
	List<int> RandomThrusts = new List<int>();
	for(int i=0; i<Math.Min(Math.Max(3, AllThrust.Count/10), AllThrust.Count); i++){
		int random_index = rnd.Next(0, AllThrust.Count);
		if(!RandomThrusts.Contains(random_index)){
			VRageMath.Base6Directions.Direction direction = AllThrust[i].Orientation.Forward;
			if(!RandomDirections.Contains(Opposite(direction))){
				RandomThrusts.Add(random_index);
				if(!RandomDirections.Contains(direction))
					RandomDirections.Add(direction);
			}
		}
	}
	for(int i=0; i<AllThrust.Count; i++){
		if(RandomThrusts.Contains(i)){
			if(evasion){
				AllThrust[i].CustomData = "evading";
				AllThrust[i].ThrustOverridePercentage  = 0.5f;
			}
		}
		else{
			if(AllThrust[i].CustomData.Equals("evading")){
				AllThrust[i].ThrustOverride = 0;
				AllThrust[i].CustomData = "";
			}
		}
	}
}

private bool UpdatedFollow = false;
private void Follow(){
	if(UpdatedFollow)
		return;
	target_position = 2 * follow_velocity * DroneTimer.TriggerDelay + follow_position;
	Vector3D expected_position = follow_position + (follow_velocity * DroneTimer.TriggerDelay);
	double speed = follow_velocity.Length();
	Vector3D movement_direction = follow_velocity;
	movement_direction.Normalize();
	double distance = (DroneRemote.GetPosition() - follow_position).Length();
	bool catching_up = (DroneRemote.GetPosition() + 5*movement_direction - follow_position).Length() < distance;
	if(distance > 200){
		if(catching_up){
			speed = Math.Min(100.0, speed + (distance / 200.0));
		}
		else {
			speed = Math.Max(1.0, speed - (distance / 200.0));
		}
	}
	follow_speed = speed;
	follow_position = expected_position;
	UpdatedFollow = true;
}

private void UpdateNavigation(){
	if((target_position - DroneRemote.CurrentWaypoint.Coords).Length() < 1)
		return;
	DroneRemote.SpeedLimit = (float) follow_speed;
	DroneRemote.FlightMode = FlightMode.OneWay;
	DroneRemote.ClearWaypoints();
	DroneRemote.AddWaypoint(target_position, "Swarming");
	DroneRemote.SetCollisionAvoidance(true);
	DroneRemote.SetDockingMode(false);
	DroneRemote.SetAutoPilotEnabled(true);
}

private void Send(string Tag, string Message){
	if(Tag.Contains(';'))
		throw new ArgumentException("Tag may not contain a semicolon (" + Tag + ')');
	IGC.SendBroadcastMessage(Tag, Message, TransmissionDistance.TransmissionDistanceMax);
	Echo("Sent Data on Tag \"" + Tag + "\":" + Message + '\n');
}

private void Send(string Tag, string Command, string Data){
	if(Command.Contains(':'))
		throw new ArgumentException("Command may not contain a colon (" + Command + ')');
	Send(Tag, Command + ':' + Data);
}

private void ParseMessage(MyIGCMessage Message){
	try{
		string Command = Message.Data.ToString().Substring(0, Message.Data.ToString().IndexOf(':'));
		string Data = Message.Data.ToString().Substring(Message.Data.ToString().IndexOf('<')+1);
		Data = Data.Substring(0, Data.IndexOf('>'));
		if(Message.Tag.Equals(DroneIdentification) && Command.Equals("Dock")){
			int index = this.Storage.IndexOf('(')+1;
			int length = this.Storage.Substring(index).IndexOf(')');
			Vector3D.TryParse(this.Storage.Substring(index, length), out DockConnector);
			
			index = this.Storage.Substring(index).IndexOf('(')+1;
			length = this.Storage.Substring(index).IndexOf(')');
			Vector3D.TryParse(this.Storage.Substring(index, length), out DockAlign);
			
			index = this.Storage.Substring(index).IndexOf('(')+1;
			length = this.Storage.Substring(index).IndexOf(')');
			Vector3D.TryParse(this.Storage.Substring(index, length), out DockApproach);
			
			TaskQueue.Clear();
			TaskQueue.Enqueue(DroneTask.Returning);
			TaskQueue.Enqueue(DroneTask.Aligning);
			TaskQueue.Enqueue(DroneTask.Waiting);
			TaskQueue.Enqueue(DroneTask.Docking);
			Echo("Received command to return to (" + DockApproach.ToString() + "), align with (" + DockAlign.ToString() + "), and dock with (" + DockConnector.ToString() + ")\n");
			NextTask();
		}
		else if(Message.Tag.Equals(DroneIdentification) && Command.Equals("Swarm")){
			int index = this.Storage.IndexOf('(')+1;
			int length = this.Storage.Substring(index).IndexOf(')');
			Vector3D.TryParse(this.Storage.Substring(index, length), out follow_position);
			
			index = this.Storage.Substring(index).IndexOf('(')+1;
			length = this.Storage.Substring(index).IndexOf(')');
			Vector3D.TryParse(this.Storage.Substring(index, length), out follow_velocity);
			
			TaskQueue.Clear();
			TaskQueue.Enqueue(DroneTask.Swarming);
			Echo("Received command to swarm the enemy currently at (" + follow_position.ToString() + ") and traveling at (" + follow_velocity.ToString() + ")\n");
			NextTask();
			following = true;
			DroneTimer.StopCountdown();
			DroneTimer.StartCountdown();
		}
		else if(Command.Equals("Stop")){
			Echo("Received command to stop\n");
			UpdateTask(DroneTask.Idle);
		}
		else{
			Echo("Unknown Command on channel \"" + Message.Tag + "\": " + Message.Data);
		}
	}
	catch(FormatException){
		Echo("Unknown Command on channel \"" + Message.Tag + "\": " + Message.Data.ToString());
	}
}

private void Scanner(){
	for(int i=0; i<listener_tags.Count; i++){
		IGC.RegisterBroadcastListener(listener_tags[i]);
	}
	List<IMyBroadcastListener> listeners = new List<IMyBroadcastListener>();
	IGC.GetBroadcastListeners(listeners);
	if(listeners.Count > 0){
		Echo("Scanning on " + listeners.Count + " listeners...");
		for(int i=0; i<listeners.Count; i++){
			Echo("\tListening on \"" + listeners[i].Tag + "\"");
			while(listeners[i].HasPendingMessage){
				MyIGCMessage message = new MyIGCMessage();
				message = listeners[i].AcceptMessage();
				ParseMessage(message);
			}
		}
		Echo("");
	}
	else{
		Echo("Not currently scanning (no listeners)\n");
	}
}

private void RunSensors(){
	List<MyDetectedEntityInfo> detected_entities = new List<MyDetectedEntityInfo>();
	DroneSensor.DetectedEntities(detected_entities);
	Echo(detected_entities.Count.ToString() + " entities detected");
	foreach(MyDetectedEntityInfo entity in detected_entities){
		if(entity.Relationship == MyRelationsBetweenPlayerAndBlock.Enemies)
			IGC.SendBroadcastMessage("SensorReport", entity.EntityId.ToString() + ";" + entity.Position + ";" + entity.Velocity, TransmissionDistance.TransmissionDistanceMax);
		else
			Echo("\tNon-Hostile Entity detected: Type " + entity.Type.ToString());
	}
	Echo("");
}

public void Main(string argument, UpdateType updateSource)
{
	Cycle_Long = (Cycle_Long + ((++Cycle)/Int64.MaxValue)) % Int64.MaxValue;
	Cycle = Cycle % Int64.MaxValue;
	switch(loadingChar){
		case '|':
			loadingChar='\\';
			break;
		case '\\':
			loadingChar='-';
			break;
		case '-':
			loadingChar='/';
			break;
		case '/':
			loadingChar='|';
			break;
	}
	Echo("Cycle " + Cycle_Long.ToString() + '-' + Cycle.ToString() + "(" + loadingChar + ")");
	Echo("Current Task: " + CurrentTask.ToString());
	Echo("Last Task: " + LastTask.ToString());
	if(TaskQueue.Count > 0){
		Echo("Next Task: " + TaskQueue.Peek());
	}
	Echo("");
	UpdatedFollow = false;
	if(DroneConnector.Status == MyShipConnectorStatus.Connected){
		UpdateTask(DroneTask.Docked);
	}
	if(argument.ToLower().Equals("Timer".ToLower()) && updateSource == UpdateType.Trigger){
		if(following)
			Follow();
		if(evasion)
			Evasion();
		if(waiting)
			NextTask();
		if(!following && !waiting && !evasion)
			DroneTimer.StopCountdown();
	}
	else {
		RunSensors();
		PerformCurrentTask();
	}
}
