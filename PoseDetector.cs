using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PoseDataCollection.PoseDataCollection;

namespace Spellotron
{
    /// <summary>
    /// Used to detect when a goal pose has been held by the user for a
    /// certian number of frames
    /// </summary>
    public class PoseDetector
    {
        //Fields
        Controller ctrl;            //Main program controller
        Pose goalPose;              //The pose that is being searched for
        int recognizedFrameCount;   //Number of consecutive frames goal pose has been recognized

        //Contants
        static readonly int maxFramesRecognized = 15; //Number of consecutive frames a user must hold goal pose
        static readonly double maxSimilarity = 92.0;  //Similarity % between frame pose and goal pose to be considered recognized

        //Accessors for private fields
        public Pose GoalPose { set { goalPose = value; } get { return goalPose; } }

        //Consturctor
        public PoseDetector(Controller c)
        {
            ctrl = c;
            recognizedFrameCount = 0;
            goalPose = new Pose(null, null);
        }

        /// <summary>
        /// Compares the pose from the current frame to the goal pose. If the pose has been
        /// held a certian number of frames, tell the controller.
        /// </summary>
        /// <param name="currentPose">The Pose from the current frame</param>
        /// <returns>The similarity of that pose to the goal pose</returns>
        public double DetectPose(Pose currentPose)
        {
            //Find similarity of each pose with current frame's pose
            double similarity = goalPose.ComparePose(currentPose);

            //If similar enough, considered it recognized this frame
            if (similarity >= maxSimilarity)
                recognizedFrameCount++;
            else
                recognizedFrameCount = 0;

            //If goal pose has been recognized for enough consecutive frames, tell ctrl
            if (recognizedFrameCount >= maxFramesRecognized)
            {
                ctrl.CorrectPoseHit();
                recognizedFrameCount = 0;
            }

            return similarity;
        }
    }
}
