﻿using System;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using OpenTK.Input;

namespace GUI.Types.Renderer
{
    internal class Camera
    {
        public Matrix4 ProjectionMatrix { get; private set; }
        public Matrix4 CameraViewMatrix { get; private set; }

        public bool MouseOverRenderArea { get; set; }
        private bool MouseDragging;

        private Vector2 MouseDelta;
        private Vector2 MousePreviousPosition;
        private Vector2 MouseSpeed = new Vector2(0f, 0f);

        private Vector3 Location;
        private double Pitch;
        private double Yaw;

        private KeyboardState KeyboardState;

        public Camera(int viewportWidth, int viewportHeight, Vector3 minBounds, Vector3 maxBounds)
        {
            SetViewportSize(viewportWidth, viewportHeight);

            Location.Y = (maxBounds.X + minBounds.X) / 2;
            Location.X = maxBounds.Y + 30.0f;
            Location.Z = maxBounds.Z + 30.0f;

            // TODO: needs fixing
            Yaw = 3f;
            Pitch = -0.9f;
        }

        public void SetViewportSize(int viewportWidth, int viewportHeight)
        {
            var aspectRatio = viewportWidth / (float)viewportHeight;
            ProjectionMatrix = Matrix4.CreatePerspectiveFieldOfView(MathHelper.PiOver4, aspectRatio, 1.0f, 4096.0f);

            // setup projection
            GL.Viewport(0, 0, viewportWidth, viewportHeight);
        }

        public void Tick()
        {
            if (!MouseOverRenderArea)
            {
                return;
            }

            var speed = KeyboardState.IsKeyDown(Key.ShiftLeft) ? 8.0f : 2.0f;

            if (KeyboardState.IsKeyDown(Key.W))
            {
                Location.X += (float)Math.Cos(Yaw) * speed;
                Location.Y += (float)Math.Sin(Yaw) * speed;
                Location.Z += (float)Pitch * speed;
            }

            if (KeyboardState.IsKeyDown(Key.S))
            {
                Location.X -= (float)Math.Cos(Yaw) * speed;
                Location.Y -= (float)Math.Sin(Yaw) * speed;
                Location.Z -= (float)Pitch * speed;
            }

            if (KeyboardState.IsKeyDown(Key.D))
            {
                Location.X -= (float)Math.Cos(Yaw + Math.PI / 2) * speed;
                Location.Y -= (float)Math.Sin(Yaw + Math.PI / 2) * speed;
            }

            if (KeyboardState.IsKeyDown(Key.A))
            {
                Location.X += (float)Math.Cos(Yaw + Math.PI / 2) * speed;
                Location.Y += (float)Math.Sin(Yaw + Math.PI / 2) * speed;
            }

            MouseSpeed.X *= 0.4f;
            MouseSpeed.Y *= 0.4f;
            MouseSpeed.X -= MouseDelta.X / 1000f;
            MouseSpeed.Y -= MouseDelta.Y / 1000f;
            MouseDelta.X = 0f;
            MouseDelta.Y = 0f;

            Yaw += MouseSpeed.X;
            Pitch += MouseSpeed.Y;

            ClampRotation();

            var lookatPoint = new Vector3((float)Math.Cos(Yaw), (float)Math.Sin(Yaw), (float)Pitch);
            CameraViewMatrix = Matrix4.LookAt(Location, Location + lookatPoint, Vector3.UnitZ);
        }

        public void HandleInput(MouseState mouseState, KeyboardState keyboardState)
        {
            KeyboardState = keyboardState;

            if (MouseOverRenderArea && mouseState.LeftButton == ButtonState.Pressed)
            {
                if (!MouseDragging)
                {
                    MouseDragging = true;
                    MousePreviousPosition = new Vector2(mouseState.X, mouseState.Y);
                }

                var mouseNewCoords = new Vector2(mouseState.X, mouseState.Y);

                MouseDelta.X = mouseNewCoords.X - MousePreviousPosition.X;
                MouseDelta.Y = mouseNewCoords.Y - MousePreviousPosition.Y;

                MousePreviousPosition = mouseNewCoords;
            }

            if (!MouseOverRenderArea || mouseState.LeftButton == ButtonState.Released)
            {
                MouseDragging = false;
            }
        }

        private void ClampRotation()
        {
            if (Pitch >= Math.PI)
            {
                Pitch = Math.PI;
            }
            else if (Pitch <= -Math.PI)
            {
                Pitch = -Math.PI;
            }

            if (Yaw >= MathHelper.TwoPi)
            {
                Yaw -= MathHelper.TwoPi;
            }
            else if (Yaw <= -MathHelper.TwoPi)
            {
                Yaw += MathHelper.TwoPi;
            }
        }
    }
}
