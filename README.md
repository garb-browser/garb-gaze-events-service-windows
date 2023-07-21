# GARB: Gaze-Aware Reading-aid for the Browser

### Requirements
1. Software requirements:
    * Visual Studios 2019 (https://visualstudio.microsoft.com/downloads/)
2. Hardware requirements:
    * Tobii 4C/5 Eyetracking device (https://gaming.tobii.com/product/tobii-eye-tracker-4c/)

### Setup
1. Clone the repo locally and open it with Visual Studios.
1. Make sure all the steps from __Setup__ are followed!
2. Turn on Tobii Eyetracking and calibrate
1. In Visual Studio open "WindowsEyeServer.sln" 
3. From the top navigation bar, run the program `Main`. You can make sure that the project configuration (right next to the start button) is set to `Debug|AnyCPU`.
4. You should see a terminal pop up with the message, "Server has started..."
5. Now open up a webpage, use the extension on it, and you should see lines of text being highlighted in blue from your eye movement!
