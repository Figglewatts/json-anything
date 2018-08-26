﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using ImGuiNET;
using JsonAnything.Util;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using OpenTK.Input;
using Vector2 = System.Numerics.Vector2;

namespace JsonAnything.GUI
{
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct ImGuiVert
    {
        public OpenTK.Vector2 pos;
        public OpenTK.Vector2 uv;
        public uint col;

        public const int PosOffset = 0;
        public const int UVOffset = 8;
        public const int ColOffset = 16;
        public static readonly int Size = sizeof(ImGuiVert);
    }
    
    public static class ImGuiRenderer
    {
        // variables
        private static float _lastScrollValue = 0;
        private static Vector2 _mousePos = Vector2.Zero;
        
        // device objects
        private static int _vertexShaderHandle, _fragmentShaderHandle, _programHandle;
        private static int _attribLocationTex;
        private static int _attribLocationProjMtx;
        private static int _attribLocationPosition;
        private static int _attribLocationUV;
        private static int _attribLocationColor;
        private static int _vboHandle;
        private static int _elementsHandle;
        private static int _fontsTextureHandle;
        
        public static void Init()
        {
            IO io = ImGui.GetIO();

            io.KeyMap[GuiKey.Tab] = (int)Key.Tab;
            io.KeyMap[GuiKey.LeftArrow] = (int)Key.Left;
            io.KeyMap[GuiKey.RightArrow] = (int)Key.Right;
            io.KeyMap[GuiKey.UpArrow] = (int)Key.Up;
            io.KeyMap[GuiKey.DownArrow] = (int)Key.Down;
            io.KeyMap[GuiKey.PageUp] = (int)Key.PageUp;
            io.KeyMap[GuiKey.PageDown] = (int)Key.PageDown;
            io.KeyMap[GuiKey.Home] = (int)Key.Home;
            io.KeyMap[GuiKey.End] = (int)Key.End;
            io.KeyMap[GuiKey.Delete] = (int)Key.Delete;
            io.KeyMap[GuiKey.Backspace] = (int)Key.BackSpace;
            io.KeyMap[GuiKey.Enter] = (int)Key.Enter;
            io.KeyMap[GuiKey.Escape] = (int)Key.Escape;
            io.KeyMap[GuiKey.A] = (int)Key.A;
            io.KeyMap[GuiKey.C] = (int)Key.C;
            io.KeyMap[GuiKey.V] = (int)Key.V;
            io.KeyMap[GuiKey.X] = (int)Key.X;
            io.KeyMap[GuiKey.Y] = (int)Key.Y;
            io.KeyMap[GuiKey.Z] = (int)Key.Z;

            io.DisplayFramebufferScale = Vector2.One;
        }

        public static void BeginFrame(double deltaTime)
        {
            IO io = ImGui.GetIO();

            io.DeltaTime = (float)deltaTime;

            if (_fontsTextureHandle <= 0)
            {
                createDeviceObjects();
            }

            updateInput();

            ImGui.NewFrame();
        }

        public static void EndFrame()
        {
            ImGui.Render();
            unsafe { renderDrawData(ImGui.GetDrawData()); }
        }

        public static void Shutdown()
        {
            destroyDeviceObjects();
        }

        public static void AddKeyChar(char keyChar)
        {
            ImGui.AddInputCharacter(keyChar);
        }

        public static void UpdateMousePos(int x, int y)
        {
            _mousePos.X = x;
            _mousePos.Y = y;
        }

        public static void Resize(int w, int h)
        {
            ImGui.GetIO().DisplaySize = new Vector2(w, h);
        }

        public static void AddFontFromFileTTF(string filename, float sizePixels, FontConfig config, char[] glyphRanges)
        {
            IO io = ImGui.GetIO();
            config.OversampleH = 1;
            config.OversampleV = 1;
            config.RasterizerMultiply = 1;
            IntPtr cnfPtr = Marshal.AllocHGlobal(Marshal.SizeOf<FontConfig>());
            Marshal.StructureToPtr(config, cnfPtr, false);
            
            unsafe
            {
                NativeFontAtlas* atlas = io.GetNativePointer()->FontAtlas;
                fixed (char* glyphs = &glyphRanges[0])
                {
                    ImGuiNative.ImFontAtlas_AddFontFromFileTTF(atlas, filename, sizePixels, cnfPtr,
                        glyphs);
                }
            }
        }

        private static void updateInput()
        {
            IO io = ImGui.GetIO();

            MouseState mouse = Mouse.GetCursorState();
            KeyboardState keyboard = Keyboard.GetState();

            for (int i = 0; i < (int)Key.LastKey; i++)
            {
                io.KeysDown[i] = keyboard[(Key)i];
            }

            io.ShiftPressed = keyboard[Key.LShift] || keyboard[Key.RShift];
            io.CtrlPressed = keyboard[Key.LControl] || keyboard[Key.RControl];
            io.AltPressed = keyboard[Key.LAlt] || keyboard[Key.RAlt];

            io.MousePosition = _mousePos;
            io.MouseDown[0] = mouse.LeftButton == ButtonState.Pressed;
            io.MouseDown[1] = mouse.RightButton == ButtonState.Pressed;
            io.MouseDown[2] = mouse.MiddleButton == ButtonState.Pressed;

            float scrollDelta = mouse.Scroll.Y - _lastScrollValue;
            io.MouseWheel = scrollDelta;
            _lastScrollValue = mouse.Scroll.Y;
        }

        private static unsafe void renderDrawData(DrawData* drawData)
        {
            // scale coordinates for retina displays
            IO io = ImGui.GetIO();
            int fbWidth = (int)(io.DisplaySize.X * io.DisplayFramebufferScale.X);
            int fbHeight = (int)(io.DisplaySize.Y * io.DisplayFramebufferScale.Y);
            if (fbWidth < 0 || fbHeight < 0) return;
            ImGui.ScaleClipRects(drawData, io.DisplayFramebufferScale);

            // backup GL state
            GL.GetInteger(GetPName.ActiveTexture, out int lastActiveTexture);
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.GetInteger(GetPName.CurrentProgram, out int lastProgram);
            GL.GetInteger(GetPName.TextureBinding2D, out int lastTexture);
            GL.GetInteger(GetPName.SamplerBinding, out int lastSampler);
            GL.GetInteger(GetPName.ArrayBufferBinding, out int lastArrayBuffer);
            GL.GetInteger(GetPName.VertexArrayBinding, out int lastVertexArray);
            int[] lastPolygonMode = new int[2]; GL.GetInteger(GetPName.PolygonMode, lastPolygonMode);
            int[] lastViewport = new int[4]; GL.GetInteger(GetPName.Viewport, lastViewport);
            int[] lastScissorBox = new int[4]; GL.GetInteger(GetPName.ScissorBox, lastScissorBox);
            GL.GetInteger(GetPName.BlendSrcRgb, out int lastBlendSrcRgb);
            GL.GetInteger(GetPName.BlendDstRgb, out int lastBlendDstRgb);
            GL.GetInteger(GetPName.BlendSrcAlpha, out int lastBlendSrcAlpha);
            GL.GetInteger(GetPName.BlendDstAlpha, out int lastBlendDstAlpha);
            GL.GetInteger(GetPName.BlendEquationRgb, out int lastBlendEquationRgb);
            GL.GetInteger(GetPName.BlendEquationAlpha, out int lastBlendEquationAlpha);
            bool lastEnableBlend = GL.IsEnabled(EnableCap.Blend);
            bool lastEnableCullFace = GL.IsEnabled(EnableCap.CullFace);
            bool lastEnableDepthTest = GL.IsEnabled(EnableCap.DepthTest);
            bool lastEnableScissorTest = GL.IsEnabled(EnableCap.ScissorTest);

            // setup GL state
            GL.Enable(EnableCap.Blend);
            GL.BlendEquation(BlendEquationMode.FuncAdd);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            GL.Disable(EnableCap.CullFace);
            GL.Disable(EnableCap.DepthTest);
            GL.Enable(EnableCap.ScissorTest);
            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);

            // setup viewport with orthographic projecton matrix
            GL.Viewport(0, 0, fbWidth, fbHeight);
            float l = 0;
            float r = io.DisplaySize.X;
            float t = 0;
            float b = io.DisplaySize.Y;
            Matrix4 orthoProjection = new Matrix4(
                2.0f / (r - l),    0,                  0,    0,
                0,                 2.0f / (t - b),     0,    0,
                0,                 0,                 -1.0f, 0,
                (r + l) / (l - r), (t + b) / (b - t),  0,    1.0f
            );

            GL.UseProgram(_programHandle);
            GL.Uniform1(_attribLocationTex, 0);
            GL.UniformMatrix4(_attribLocationProjMtx, false, ref orthoProjection);
            GL.BindSampler(0, 0);
            
            // recreate the VAO every time
            int vaoHandle = GL.GenVertexArray();
            GL.BindVertexArray(vaoHandle);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vboHandle);
            GL.EnableVertexAttribArray(_attribLocationPosition);
            GL.EnableVertexAttribArray(_attribLocationUV);
            GL.EnableVertexAttribArray(_attribLocationColor);
            GL.VertexAttribPointer(_attribLocationPosition, 2, VertexAttribPointerType.Float, false,
                ImGuiVert.Size, 
                ImGuiVert.PosOffset);
            GL.VertexAttribPointer(_attribLocationUV, 2, VertexAttribPointerType.Float, false, 
                ImGuiVert.Size,
                ImGuiVert.UVOffset);
            GL.VertexAttribPointer(_attribLocationColor, 4, VertexAttribPointerType.UnsignedByte, false, 
                ImGuiVert.Size,
                ImGuiVert.ColOffset);

            // draw
            Vector2 pos = new Vector2(l, t);
            for (int n = 0; n < drawData->CmdListsCount; n++)
            {
                var cmdList = drawData->CmdLists[n];
                int indexBufferOffset = 0;

                GL.BindBuffer(BufferTarget.ArrayBuffer, _vboHandle);
                GL.BufferData(BufferTarget.ArrayBuffer, cmdList->VtxBuffer.Size * ImGuiVert.Size,
                    new IntPtr(cmdList->VtxBuffer.Data), BufferUsageHint.StreamDraw);

                GL.BindBuffer(BufferTarget.ElementArrayBuffer, _elementsHandle);
                GL.BufferData(BufferTarget.ElementArrayBuffer, cmdList->IdxBuffer.Size * sizeof(ushort),
                    new IntPtr(cmdList->IdxBuffer.Data), BufferUsageHint.StreamDraw);

                DrawCmd* cmds = (DrawCmd*)cmdList->CmdBuffer.Data;
                for (int cmdIndex = 0; cmdIndex < cmdList->CmdBuffer.Size; cmdIndex++)
                {
                    DrawCmd* drawCmd = &cmds[cmdIndex];
                    
                    Vector4 clipRect = new Vector4(
                        drawCmd->ClipRect.X - pos.X,
                        drawCmd->ClipRect.Y - pos.Y,
                        drawCmd->ClipRect.Z - pos.X,
                        drawCmd->ClipRect.W - pos.Y
                    );
                    GL.Scissor((int)clipRect.X, (int)(fbHeight - clipRect.W), (int)(clipRect.Z - clipRect.X),
                        (int)(clipRect.W - clipRect.Y));

                    GL.BindTexture(TextureTarget.Texture2D, drawCmd->TextureId.ToInt32());
                    GL.DrawElements(BeginMode.Triangles, (int)drawCmd->ElemCount,
                        DrawElementsType.UnsignedShort,
                        indexBufferOffset);

                    indexBufferOffset += (int)drawCmd->ElemCount * 2;
                }
            }
            GL.DeleteVertexArray(vaoHandle);

            // restore modified GL state
            GL.UseProgram(lastProgram);
            GL.BindTexture(TextureTarget.Texture2D, lastTexture);
            GL.BindSampler(0, lastSampler);
            GL.ActiveTexture((TextureUnit)lastActiveTexture);
            GL.BindVertexArray(lastVertexArray);
            GL.BindBuffer(BufferTarget.ArrayBuffer, lastArrayBuffer);
            GL.BlendEquationSeparate((BlendEquationMode)lastBlendEquationRgb,
                (BlendEquationMode)lastBlendEquationAlpha);
            GL.BlendFuncSeparate((BlendingFactorSrc)lastBlendSrcRgb, (BlendingFactorDest)lastBlendDstRgb,
                (BlendingFactorSrc)lastBlendSrcAlpha, (BlendingFactorDest)lastBlendDstAlpha);
            if (lastEnableBlend) GL.Enable(EnableCap.Blend); else GL.Disable(EnableCap.Blend);
            if (lastEnableCullFace) GL.Enable(EnableCap.CullFace); else GL.Disable(EnableCap.CullFace);
            if (lastEnableDepthTest) GL.Enable(EnableCap.DepthTest); else GL.Disable(EnableCap.DepthTest);
            if (lastEnableScissorTest) GL.Enable(EnableCap.ScissorTest); else GL.Disable(EnableCap.ScissorTest);
            GL.PolygonMode(MaterialFace.FrontAndBack, (PolygonMode)lastPolygonMode[0]);
            GL.Viewport(lastViewport[0], lastViewport[1], lastViewport[2], lastViewport[3]);
            GL.Scissor(lastScissorBox[0], lastScissorBox[1], lastScissorBox[2], lastScissorBox[3]);
        }

        private static void createDeviceObjects()
        {
            // backup GL state
            GL.GetInteger(GetPName.TextureBinding2D, out int lastTexture);
            GL.GetInteger(GetPName.ArrayBufferBinding, out int lastArrayBuffer);
            GL.GetInteger(GetPName.VertexArrayBinding, out int lastVertexArray);

            const string vertexShader = @"
                #version 130
                uniform mat4 ProjMtx;
                in vec2 Position;
                in vec2 UV;
                in vec4 Color;
                out vec2 Frag_UV;
                out vec4 Frag_Color;
                void main()
                {
                    Frag_UV = UV;
                    Frag_Color = Color / 255.0;
                    gl_Position = ProjMtx * vec4(Position.xy, 0, 1);
                }
            ";

            const string fragmentShader = @"
                #version 130
                uniform sampler2D Texture;
                in vec2 Frag_UV;
                in vec4 Frag_Color;
                out vec4 Out_Color;
                void main()
                {
                    Out_Color = Frag_Color * texture(Texture, Frag_UV.st);
                }
            ";

            // create shaders
            _vertexShaderHandle = GL.CreateShader(ShaderType.VertexShader);
            GL.ShaderSource(_vertexShaderHandle, vertexShader);
            GL.CompileShader(_vertexShaderHandle);
            checkShader(_vertexShaderHandle, "vertex");

            _fragmentShaderHandle = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(_fragmentShaderHandle, fragmentShader);
            GL.CompileShader(_fragmentShaderHandle);
            checkShader(_fragmentShaderHandle, "fragment");

            _programHandle = GL.CreateProgram();
            GL.AttachShader(_programHandle, _vertexShaderHandle);
            GL.AttachShader(_programHandle, _fragmentShaderHandle);
            GL.LinkProgram(_programHandle);
            checkProgram(_programHandle, "main");

            _attribLocationTex = GL.GetUniformLocation(_programHandle, "Texture");
            _attribLocationProjMtx = GL.GetUniformLocation(_programHandle, "ProjMtx");
            _attribLocationPosition = GL.GetAttribLocation(_programHandle, "Position");
            _attribLocationUV = GL.GetAttribLocation(_programHandle, "UV");
            _attribLocationColor = GL.GetAttribLocation(_programHandle, "Color");

            // create buffers
            _vboHandle = GL.GenBuffer();
            _elementsHandle = GL.GenBuffer();

            createFontsTexture();

            // restore modified openGL state
            GL.BindTexture(TextureTarget.Texture2D, lastTexture);
            GL.BindBuffer(BufferTarget.ArrayBuffer, lastArrayBuffer);
            GL.BindVertexArray(lastVertexArray);
        }

        private static void checkShader(int handle, string name)
        {
            GL.GetShader(handle, ShaderParameter.CompileStatus, out int status);
            GL.GetShader(handle, ShaderParameter.InfoLogLength, out int logLength);
            if (status == 0)
            {
                Logger.Log()(LogLevel.ERR, "ERROR: Failed to compile ImGui {0} shader", name);
            }

            if (logLength > 0)
            {
                GL.GetShaderInfoLog(handle, out string infoLog);
                Logger.Log()(LogLevel.ERR, "Shader info log: {0}", infoLog);
            }
        }

        private static void checkProgram(int handle, string name)
        {
            GL.GetProgram(handle, GetProgramParameterName.LinkStatus, out int status);
            GL.GetProgram(handle, GetProgramParameterName.InfoLogLength, out int logLength);
            if (status == 0)
            {
                Logger.Log()(LogLevel.ERR, "ERROR: Failed to link ImGui {0} shader program", name);
            }

            if (logLength > 0)
            {
                GL.GetProgramInfoLog(handle, out string infoLog);
                Logger.Log()(LogLevel.ERR, "Program info log: {0}", infoLog);
            }
        }

        private static void destroyDeviceObjects()
        {
            if (_vboHandle > 0) GL.DeleteBuffer(_vboHandle);
            if (_elementsHandle > 0) GL.DeleteBuffer(_elementsHandle);
            _vboHandle = _elementsHandle = 0;

            if (_programHandle > 0 && _vertexShaderHandle > 0) GL.DetachShader(_programHandle, _vertexShaderHandle);
            if (_vertexShaderHandle > 0) GL.DeleteShader(_vertexShaderHandle);
            _vertexShaderHandle = 0;

            if (_programHandle > 0 && _fragmentShaderHandle > 0) GL.DetachShader(_programHandle, _fragmentShaderHandle);
            if (_fragmentShaderHandle > 0) GL.DeleteShader(_fragmentShaderHandle);
            _fragmentShaderHandle = 0;

            if (_programHandle > 0) GL.DeleteProgram(_programHandle);
            _programHandle = 0;

            destroyFontsTexture();
        }

        private static void createFontsTexture()
        {
            // build texture atlas
            IO io = ImGui.GetIO();
            FontTextureData pixels = io.FontAtlas.GetTexDataAsRGBA32();

            // copy data to managed array
            byte[] pixelsArray = new byte[pixels.Width * pixels.Height * pixels.BytesPerPixel];
            unsafe { Marshal.Copy(new IntPtr(pixels.Pixels), pixelsArray, 0, pixelsArray.Length); }

            // create texture
            GL.GetInteger(GetPName.TextureBinding2D, out int lastTexture);
            _fontsTextureHandle = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, _fontsTextureHandle);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter,
                (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter,
                (int)TextureMagFilter.Linear);
            GL.PixelStore(PixelStoreParameter.UnpackRowLength, 0);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, pixels.Width, pixels.Height, 0,
                PixelFormat.Rgba, PixelType.UnsignedByte, pixelsArray);

            io.FontAtlas.SetTexID(_fontsTextureHandle);
            io.FontAtlas.ClearTexData();

            // restore state
            GL.BindTexture(TextureTarget.Texture2D, lastTexture);
        }

        private static void destroyFontsTexture()
        {
            if (_fontsTextureHandle > 0)
            {
                IO io = ImGui.GetIO();
                GL.DeleteTexture(_fontsTextureHandle);
                io.FontAtlas.SetTexID(0);
                _fontsTextureHandle = 0;
            }
        }
    }
}
