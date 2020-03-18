using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Kinect;

namespace Spellotron
{
    /// <summary>
    /// Handles the video (colorstream) and user (skeleton) data coming from
    /// the Kinect sensor.
    /// </summary>
    class VisualStreamProcessor
    {
        //Fields
        private Controller ctrl;
        private SkeletonFrame skeletonFrame;
        private ColorImageFrame colorFrame;
        private Skeleton[] skeletonData;
        private Skeleton currentSkeleton;
        private byte[] colorPixelData;
        private WriteableBitmap colorBitmap;
        private SolidColorBrush trackedBrush;
        private SolidColorBrush inferredBrush;
        private SolidColorBrush oldSkeletonBrush;
        private LinearGradientBrush trackedToInferredBrush;
        private LinearGradientBrush inferredToTrackedBrush;
        private DrawingGroup colorAndSkeletonDG;
        private DrawingImage colorAndSkeletonDI;
        private Image colorAndSkeletonImage;
        private DrawingGroup skeletonDG;
        private DrawingImage skeletonDI;
        private Image skeletonImage;
        private Image colorImage;
        private static int boneThickness = 6;
        private static int jointRadius = 8;
        private static int imageHeight = 480;
        private static int imageWidth = 640;
        private bool freshSkeletonImg = true;

        //Accessors for private fields
        public ColorImageFrame ColorFrame { set { colorFrame = value; } }
        public SkeletonFrame SkeletonFrame { set { skeletonFrame = value; } }
        public Skeleton CurrentSkeleton { get { return currentSkeleton; } }

        //Constructor
        public VisualStreamProcessor(Controller c, int asize)
        {
            ctrl = c;
            skeletonData = new Skeleton[asize];

            //Set up graphics
            colorAndSkeletonImage = new Image();
            skeletonImage = new Image();
            colorImage = new Image();
            Color trackedColor = Color.FromRgb(0, 175, 0);
            Color inferredColor = Color.FromRgb(255, 0, 0);
            Color oldSkeletonColor = Colors.White;
            Color multiTrackingStateColor = Color.FromRgb(255, 255, 0);

            trackedBrush = new SolidColorBrush(trackedColor);
            inferredBrush = new SolidColorBrush(inferredColor);
            oldSkeletonBrush = new SolidColorBrush(oldSkeletonColor);

            trackedToInferredBrush = new LinearGradientBrush();
            trackedToInferredBrush.GradientStops.Add(new GradientStop(trackedColor, 0.25));
            trackedToInferredBrush.GradientStops.Add(new GradientStop(multiTrackingStateColor, 0.50));
            trackedToInferredBrush.GradientStops.Add(new GradientStop(inferredColor, 1.0));

            inferredToTrackedBrush = new LinearGradientBrush();
            inferredToTrackedBrush.GradientStops.Add(new GradientStop(inferredColor, 0.25));
            inferredToTrackedBrush.GradientStops.Add(new GradientStop(multiTrackingStateColor, 0.50));
            inferredToTrackedBrush.GradientStops.Add(new GradientStop(trackedColor, 1.0));

            colorAndSkeletonDG = new DrawingGroup();
            colorAndSkeletonDI = new DrawingImage(colorAndSkeletonDG);
            colorAndSkeletonImage.Source = colorAndSkeletonDI;
            skeletonDG = new DrawingGroup();
            skeletonDI = new DrawingImage(skeletonDG);
            skeletonImage.Source = skeletonDI;

            //Bitmap to hold the color and skeleton image
            colorBitmap = new WriteableBitmap(
                             imageWidth, // Width
                             imageHeight, // Height
                             96,  // DpiX
                             96,  // DpiY
                             PixelFormats.Bgr32,
                             null);
        }

        /// <summary>
        /// Processes the current skeleton frame from the Kinect.
        /// Finds the first active user skeleton and makes it the current tracked skeleton
        /// </summary>
        public void ProcessSkeleton()
        {
            if (skeletonFrame != null)
            {
                currentSkeleton = null;
                skeletonFrame.CopySkeletonDataTo(skeletonData);
                foreach (Skeleton s in skeletonData)
                {                    
                    if (s != null && s.TrackingState == SkeletonTrackingState.Tracked)
                    {
                        currentSkeleton = s;
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// Renders a frame of video from the Kinect's color stream
        /// </summary>
        /// <returns>The video frame</returns>
        public Image RenderColorImage()
        {            
            if (colorFrame != null)
            {
                if (colorPixelData == null) //First time array initialization
                    colorPixelData = new byte[colorFrame.PixelDataLength];
                colorFrame.CopyPixelDataTo(colorPixelData);
                colorBitmap.WritePixels(
                       new Int32Rect(0, 0, colorBitmap.PixelWidth, colorBitmap.PixelHeight),
                       colorPixelData, colorBitmap.PixelWidth * sizeof(int), 0);
                colorImage.Source = colorBitmap;
            }
            return colorImage;
        }

        /// <summary>
        /// Renders a frame of video from the Kinect's color stream, then
        /// creates an representation of the user's skeleton and overlays
        /// it on the video frame.
        /// </summary>
        /// <returns>The video frame with the skeleton overlayed</returns>
        public Image RenderColorAndSkeletonImage()
        {
            //Render the color frame
            if (colorFrame != null)
            {
                if (colorPixelData == null) //First time array initialization
                    colorPixelData = new byte[colorFrame.PixelDataLength];
                colorFrame.CopyPixelDataTo(colorPixelData);
                colorBitmap.WritePixels(
                       new Int32Rect(0, 0, colorBitmap.PixelWidth, colorBitmap.PixelHeight),
                       colorPixelData, colorBitmap.PixelWidth * sizeof(int), 0);
            }

            //Draw skeleton components of the image
            using (DrawingContext dc = this.colorAndSkeletonDG.Open())
            {
                dc.DrawImage(colorBitmap, new Rect(0.0, 0.0, imageWidth, imageHeight));
                if (currentSkeleton != null && (currentSkeleton.TrackingState == SkeletonTrackingState.Tracked))
                    DrawBonesAndJoints(currentSkeleton, dc);
            }
            this.colorAndSkeletonDG.ClipGeometry = new RectangleGeometry(new Rect(0.0, 0.0, imageWidth, imageHeight));

            return colorAndSkeletonImage;
        }

        /// <summary>
        /// Renders an image of a passed skeleton. If no skeleton is passed as a parameter
        /// (passedSkeleton == null) then the skeleton in the currentSkeleton field
        /// is rendered.
        /// </summary>
        /// <param name="passedSkeleton">The skeleton to render an image of (can be null)</param>
        /// <returns>Image of the skeleton</returns>
        public Image RenderSkeletonImage(Skeleton passedSkeleton)
        {
            Skeleton skel = currentSkeleton;
            if (passedSkeleton != null)
            {
                skel = passedSkeleton;
                freshSkeletonImg = false;
            }
            //Draw skeleton components of the image
            using (DrawingContext dc = this.skeletonDG.Open())
            {
                if (skel != null && (skel.TrackingState == SkeletonTrackingState.Tracked))
                    DrawBonesAndJoints(skel, dc);
            }
            this.skeletonDG.ClipGeometry = new RectangleGeometry(new Rect(0.0, 0.0, imageWidth, imageHeight));
            freshSkeletonImg = true;

            return skeletonImage;
        }

        /// <summary>
        /// Draws the joints and bones of the skeleton on to the passed drawing context.
        /// </summary>
        /// <param name="skeleton">The skeleton to draw</param>
        /// <param name="drawingContext">The drawing context to draw to</param>
        private void DrawBonesAndJoints(Skeleton skeleton, DrawingContext drawingContext)
        {
            //Torso
            this.DrawBone(skeleton, drawingContext, JointType.Head, JointType.ShoulderCenter);
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.ShoulderLeft);
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.ShoulderRight);
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.Spine);
            this.DrawBone(skeleton, drawingContext, JointType.Spine, JointType.HipCenter);
            this.DrawBone(skeleton, drawingContext, JointType.HipCenter, JointType.HipLeft);
            this.DrawBone(skeleton, drawingContext, JointType.HipCenter, JointType.HipRight);

            // Left Arm
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderLeft, JointType.ElbowLeft);
            this.DrawBone(skeleton, drawingContext, JointType.ElbowLeft, JointType.WristLeft);
            this.DrawBone(skeleton, drawingContext, JointType.WristLeft, JointType.HandLeft);

            // Right Arm
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderRight, JointType.ElbowRight);
            this.DrawBone(skeleton, drawingContext, JointType.ElbowRight, JointType.WristRight);
            this.DrawBone(skeleton, drawingContext, JointType.WristRight, JointType.HandRight);

            // Left Leg
            this.DrawBone(skeleton, drawingContext, JointType.HipLeft, JointType.KneeLeft);
            this.DrawBone(skeleton, drawingContext, JointType.KneeLeft, JointType.AnkleLeft);
            this.DrawBone(skeleton, drawingContext, JointType.AnkleLeft, JointType.FootLeft);

            // Right Leg
            this.DrawBone(skeleton, drawingContext, JointType.HipRight, JointType.KneeRight);
            this.DrawBone(skeleton, drawingContext, JointType.KneeRight, JointType.AnkleRight);
            this.DrawBone(skeleton, drawingContext, JointType.AnkleRight, JointType.FootRight);

            //Draw the Joints
            foreach (Joint j in skeleton.Joints)
            {
                Brush drawBrush = trackedBrush;
                int jr = jointRadius;

                if (j.TrackingState == JointTrackingState.Inferred)
                    drawBrush = inferredBrush;

                if (!freshSkeletonImg)
                {
                    drawBrush = oldSkeletonBrush;
                    jr = jr - 2;
                }

                drawingContext.DrawEllipse(drawBrush, null, ctrl.MapSkeletonPoint(j.Position), jr, jr);
            }
        }

        /// <summary>
        /// Draws a bone between two joints to the passed drawing context.
        /// </summary>
        /// <param name="skeleton">The skeleton being drawn</param>
        /// <param name="drawingContext">The drawing context to draw to</param>
        /// <param name="jointType0">First end joint</param>
        /// <param name="jointType1">Second end joint</param>
        private void DrawBone(Skeleton skeleton, DrawingContext drawingContext, JointType jointType0, JointType jointType1)
        {
            Joint joint0 = skeleton.Joints[jointType0];
            Joint joint1 = skeleton.Joints[jointType1];
            Pen drawPen = new Pen(inferredBrush, boneThickness);

            if (joint0.TrackingState == JointTrackingState.Tracked && joint1.TrackingState == JointTrackingState.Tracked)
                drawPen = new Pen(trackedBrush, boneThickness);

            if (joint0.TrackingState == JointTrackingState.Tracked && joint1.TrackingState == JointTrackingState.Inferred)
                drawPen = new Pen(trackedToInferredBrush, boneThickness);

            if (joint0.TrackingState == JointTrackingState.Inferred && joint1.TrackingState == JointTrackingState.Tracked)
                drawPen = new Pen(inferredToTrackedBrush, boneThickness);

            if(!freshSkeletonImg)
                drawPen = new Pen(oldSkeletonBrush, boneThickness - 2);

            drawingContext.DrawLine(drawPen, ctrl.MapSkeletonPoint(joint0.Position), ctrl.MapSkeletonPoint(joint1.Position));
        }
    }
}
