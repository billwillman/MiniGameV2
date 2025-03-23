Integration with RPGBuilder v2.0 by Blink Studios 
https://assetstore.unity.com/packages/tools/game-toolkits/rpg-builder-177657

0) Import my asset from the Package Manager (Window > Package Manager > My Assets)
1) Import Unity's new Input System with it
2) Activate the new input system by going to Edit > Project Settings > Player > Active Input Handling and selecting "Both" from the drop-down list
3) Import the files of the provided integration unitypackage (Integration > RPGBuilder) by double-clicking it and pressing the "Import" button
4) Assign the provided prefab(s) inside the RPG Builder editor at the top (BLINK > RPG Builder) under Character > Races > Genders
5) Uncheck "Dynamic Animator" and Save
6) Done

-- optional --
Usage of RPG Builder Action Keys)
    1) Check "UseRPGBuilderActionKeys" of the RPGControllerIntegration component
    2) Set up the additional action keys which you need but are not provided by default (BLINK > RPG Builder > Settings > General > Action Keys > Action Key List):
        StrafeLeft
        StrafeRight
        RotateLeft
        RotateRight
        RotationModifier
        MoveForwardHalf1
        MoveForwardHalf2
        Dive
        Surface
        ToggleAutorunning
        ToggleWalking
        ToggleCrouching
        CancelClimbing
        AlignWithCamera
        PauseCameraRotation
    You can also see how they are used if you open the RPGControllerIntegration script and scroll to method "GetInputs"

Input Action: Toggle Menu Cursor)
    It is recommended to unbind input action "Toggle Menu Cursor" in Scripts > Inputs > RPGInputActions to prevent interferences with RPGBuilder logic. The functionality behind this input action is already connected to the RPGBuilder UI menu/panel behavior. Whenever an UI panel is opened, the menu cursor is automatically enabled, and disabled once all panels are closed again

Mounts) 
    You need to assign an animator override controller to the mount effect (under "Animator Controller") which has the CharacterIntegration animator as basis. See folder Integration > RPGBuilder > Animator Controllers > Overrides for an example for a bear mount animator override

Shapeshifting) 
    Assign the provided example Animator Controller BearShapeshift to the corresponding shapeshifting effect

Since it is hard for me to test every single RPG Builder feature, please report any integration bugs you find via https://johnstairs.com/rcc/#contact or write me in the Blink Discord - thanks!

Best regards,
John