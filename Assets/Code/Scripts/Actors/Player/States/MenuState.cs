using UnityEngine;

public class MenuState : State<Player>
{
    public MenuState(Player player, StateMachine<Player> stateMachine) : base(player, stateMachine) { }

    public override void Enter()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public override void Exit()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public override void LogicUpdate()
    {
        if (Owner.Input.Player.Pause.WasPressedThisFrame())
        {
            StateMachine.ChangeState(Owner.GroundState);
            return;
        }
    }
}