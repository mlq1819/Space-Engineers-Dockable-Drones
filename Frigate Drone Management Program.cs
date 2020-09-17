
public struct EnemyShip{
	long EntityId;
	Vector3D position;
	Vector3D velocity;
	public EnemyShip(long e, Vector3D p, Vector3D v){
		EntityId = e;
		position = p;
		velocity = v;
	}
}

public enum SwarmStatus{
	Docking = 1,
	Defending = 2,
	Attacking = 3
}


private List<IMyProgrammableBlock> DockPrograms = new List<IMyProgrammableBlock>();
private IMyCockpit FlightDeck = null; //Block must be named "Command Seat"
private IMyTimerBlock FlightTimer = null; //Block must be named "Flight Timer Block"

private List<string> listener_tags = new List<string>{"Zihl Combat Assist Drone", "SensorReport"};
private List<string> DroneIDs = new List<string>();
private List<Vector3D> RelativeChasePositions = new List<Vector3D>();
private List<EnemyShip> EnemyShips = new List<EnemyShip>();

private SwarmStatus Status = SwarmStatus.Docking;

private double guess_distance;
private Vector3D follow_position = new Vector3D(0,0,0);
private Vector3D follow_velocity = new Vector3D(0,0,0);

private long Cycle = 0;
private long Cycle_Long = 1;
private char loadingChar = '|';


private bool SetBlocks(){
	DockPrograms = new List<IMyProgrammableBlock>();
	List<IMyProgrammableBlock> AllProgBlocks = new List<IMyProgrammableBlock>();
	GridTerminalSystem.GetBlocksOfType<IMyProgrammableBlock>(AllProgBlocks);
	foreach(IMyProgrammableBlock block in AllProgBlocks){
		if(block.CustomName.ToLower().Equals("Dock Programmable block".ToLower())){
			block.CustomName = "Dock Programmable block";
			DockPrograms.Add(block);
		}
	}
	FlightDeck = (IMyCockpit) GridTerminalSystem.GetBlockWithName("Command Seat");
	if(FlightDeck == null){
		Echo("Could not find FlightDeck (CustomName must be set to \"Command Seat\"");
		return false;
	}
	FlightTimer = (IMyTimerBlock) GridTerminalSystem.GetBlockWithName("Flight Timer Block");
	if(FlightTimer == null){
		Echo("Could not find FlightTimer (CustomName must be set to \"Flight Timer Block\"");
		return false;
	}
	
	return true;
}

public Program()
{
    if(SetBlocks()){
		Runtime.UpdateFrequency = UpdateFrequency.Update100;
		try{
			int index = this.Storage.IndexOf("\nEnemies");
			string[] Data = this.Storage.Substring(0, index).Trim().Split(';', StringSplitOptions.RemoveEmptyEntries);
			Status = (SwarmStatus) Int32.Parse(Data[0].Trim());
			Vector3D.TryParse(Data[1], out follow_position);
			Vector3D.TryParse(Data[2], out follow_velocity);
			
			index += "\nEnemies".Length();
			Data = this.Storage.Substring(index).Trim().Split('\n', StringSplitOptions.RemoveEmptyEntries);
			foreach(string str in Data){
				string[] Subdata = str.Trim().Split(';', StringSplitOptions.RemoveEmptyEntries);
				long EntityId = Int64.Parse(Subdata[0]);
				Vector3D p, v;
				Vector3D.TryParse(Subdata[1], out p);
				Vector3D.TryParse(Subdata[2], out v);
				EnemyShips.Add(new EnemyShip(EntityId, p, v));
			}
		}
		catch(Exception){
			this.Storage = "";
			Echo("Wiped Storage\n");
		}
	}
	else {
		Runtime.UpdateFrequency = UpdateFrequency.None;
	}
}

public void Save()
{
	this.Storage = ((int)Status).ToString() + ';' + follow_position.ToString() + ';' + follow_velocity.ToString();
	this.Storage += "\nEnemies";
	foreach(EnemyShip enemy in EnemyShips){
		this.Storage += '\n' + enemy.EntityId.ToString() + ';' + enemy.position.ToString() + ';' + enemy.velocity.ToString();
	}
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
		//TODO - add commands here to parse
		if(Message.Tag.Equals("Zihl Combat Assist Drone") && (Command.Equals("Started") || Command.Equals("NewID"))){
			listener_tags.Add(Data);
			DroneIDs.Add(Data);
			Random rnd = new Random();
			Vector3D direction = new Vector3D(rnd.Next(0, 100)-50, rnd.Next(0, 100)-50, rnd.Next(0, 100)-50);
			direction.Normalize();
			int distance = 200 + 50 * rnd.Next(0, 6);
			RelativeChasePositions.Add(distance * direction);
			Echo("Found new drone with ID \"" + Data + "\"\n");
		}
		else if(Message.Tag.Equals("SensorReport")){
			string[] subdata = Data.Split(';', StringSplitOptions.RemoveEmptyEntries);
			long EntityId = Int64.Parse(subdata[0].Trim());
			Vector3D enemy_position, enemy_velocity;
			Vector3D.TryParse(subdata[1], out enemy_position);
			Vector3D.TryParse(subdata[2], out enemy_velocity);
			bool has_data_on_record = false;
			for(int i=0; i<EnemyShips.Count; i++){
				if(EnemyShips[i] == EntityId){
					has_data_on_record = true;
					EnemyShips[i].position = enemy_position;
					EnemyShips[i].velocity = enemy_velocity;
				}
			}
			if(!has_data_on_record){
				EnemyShips.Add(new EnemyShip(EntityId, enemy_position, enemy_velocity));
			}
			if(Status == SwarmStatus.Attacking && (follow_position - enemy_position).Length() < 800){
				follow_position = enemy_position;
				follow_velocity = enemy_velocity;
				FlightTimer.StopCountdown();
				FlightTimer.StartCountdown();
				SwarmAll();
			}
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

public void TweakGuess(double increase_by){
	guess_distance += increase_by;
}

public void ConfirmGuess(){
	Status = SwarmStatus.Attacking;
	follow_velocity = FlightDeck.GetShipVelocities().LinearVelocity;
	Vector3D direction = follow_velocity;
	direction.Normalize();
	follow_position = FlightDeck.GetPosition() + (guess_distance * direction);
	SwarmAll();
	FlightTimer.StartCountdown();
	guess_distance = 0;
}

public void EnvoyAll(){
	Status = SwarmStatus.Defending;
	follow_position = Me.CubeGrid.GetPosition();
	follow_velocity = FlightDeck.GetShipVelocities().LinearVelocity;
	SwarmAll();
	FlightTimer.StartCountdown();
}

public void SwarmAll(){
	for(int i=0; i<DroneIDs.Count; i++){
		Vector3D position = follow_position + RelativeChasePositions[i];
		Send(DroneIDs[i], "Swarm", '(' + position.ToString() + ");(" + follow_velocity.ToString() + ')');
		Echo("Instructed " + DroneIDs[i] + " to Swarm near " + position.ToString());
	}
}

public void DockAll(){
	FlightTimer.StopCountdown();
	if(FlightDeck.GetShipSpeed() < 0.01){
		List<bool> CanAcceptDock = new List<bool>();
		Status = SwarmStatus.Docking;
		int count = 0;
		for(int i=0; i<DockPrograms.Count; i++){
			if(DockPrograms.CustomData.Length>0){
				string[] Data = DockPrograms.CustomData.Split('\n', StringSplitOptions.RemoveEmptyEntries);
				if(Data[0].Equals("Undocked")){
					count++;
					CanAcceptDock.Add(true);
				}
				else{
					CanAcceptDock.Add(false);
				}
			}
			else {
				CanAcceptDock.Add(false);
			}
		}
		for(int i=0; i<count && i<DroneIDs.Count; i++){
			for(int j=0; j<CanAcceptDock.Count; j++){
				if(CanAcceptDock[j]){
					string[] Data = DockPrograms.CustomData.Split('\n', StringSplitOptions.RemoveEmptyEntries);
					Send(DroneIDs[i], "Dock", '(' + Data[1] + ");(" + Data[2] + ");(" + Data[3] + ')');
					Echo("Instructed " + DroneIDs[i] + " to Dock at " + Data[3]);
					CanAcceptDock[j]=false;
					break;
				}
			}
		}
	}
	else {
		Echo("Cannot Dock: Ship Speed too high (" + FlightDeck.GetShipSpeed().ToString() + " mps)\n");
	}
}

public void StopAll(){
	Send("Zihl Combat Assist Drone", "Stop", "");
	Echo("Stopped all Combat Drones\n");
}

private bool UpdatedFollow = false;
private void Follow(){
	if(UpdatedFollow)
		return;
	if(Status == SwarmStatus.Defending){
		follow_position = Me.CubeGrid.GetPosition();
		follow_velocity = FlightDeck.GetShipVelocities().LinearVelocity;
	}
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

public void Main(string argument, UpdateType updateSource)
{
	UpdatedFollow = false;
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
	Echo("Swarm Status: " + Status.ToString() + '\n');
	if(argument.ToLower().Equals("Timer".ToLower()) && updateSource == UpdateType.Trigger){
		if(Status == SwarmStatus.Defending)
			EnvoyAll();
	}
	else if(argument.ToLower().Equals("defend")){
		EnvoyAll();
	}
	else if(argument.ToLower().Equals("dock")){
		DockAll();
	}
	else if(argument.ToLower().Equals("confirm")){
		ConfirmGuess();
	}
	else if(argument.ToLower().Contains("tweak:")){
		try{
			double tweak = double.Parse(argument.Substring(argument.ToLower().IndexOf("tweak:") + "tweak:".Length).Trim());
			TweakGuess(tweak);
		}
		catch(Exception){
			Echo("Invalid argument\n");
		}
	}
}
