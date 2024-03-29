﻿//
//  Author:
//    Dominik Madarász combatwz.sk@gmail.com
//
//  Copyright (c) 2015, ZaKlaus
//
//  All rights reserved.
//
//  Redistribution and use in source and binary forms, with or without modification, are permitted provided that the following conditions are met:
//
//     * Redistributions of source code must retain the above copyright notice, this list of conditions and the following disclaimer.
//     * Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer in
//       the documentation and/or other materials provided with the distribution.
//     * Neither the name of the [ORGANIZATION] nor the names of its contributors may be used to endorse or promote products derived from this software without specific prior written permission.
//
//  THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
//  "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
//  LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
//  A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR
//  CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
//  EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
//  PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
//  PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
//  LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
//  NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
//  SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
//
using System;
using System.Collections.Generic;
using System.Collections;
using System.Drawing;
using System.Linq;
using System.Text;
using System.IO;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using OpenTK.Input;

namespace Demax
{

    public static class DefaultShader
    {
        public static string passthrough = 
            "#version 330\n"+
            "in vec3 vPosition;\n"+
            "out vec2 UV;\n"+

            "void main(){\n"+
	        "    gl_Position = vec4(vPosition,1);\n"+
            "    UV = (vPosition.xy+vec2(1,1))/2.0;\n" +
            "}\n"+
            "";

        public static string f_passthrough =
            "#version 330\n" +
            "in vec2 UV;\n" +
            "out vec3 color;\n" +

            "uniform sampler2D renderedTexture;\n" +
            "uniform float time;\n" +

            "void main(){\n" +
            "    color = texture(renderedTexture, UV).xyz;\n" +
            "}\n" +
            "";

        public static string vs_fallback =
            "#version 330\n" +
            "in vec3 vPosition;\n" +
            "in vec2 texcoord;\n" +
            "out vec2 f_texcoord;\n" +
            "uniform mat4 M;\n" +
            "uniform mat4 V;\n" +
            "uniform mat4 P;\n" +
            "uniform mat4 MVP;\n"+
            "void main(){\n" +
                "gl_Position = MVP * vec4(vPosition,1);\n" +
                "f_texcoord = texcoord;"+
            "}\n" +
            "";

        public static string fs_fallback =
            "#version 330\n" +
            "in vec2 f_texcoord;\n" +
            "out vec4 outputColor;\n" +
            "uniform sampler2D maintexture;\n" +
            "void main()\n" +
            "{\n" +
                "vec2 flipped_texcoord = vec2(f_texcoord.x, 1.0 - f_texcoord.y);\n" +
                "outputColor = texture(maintexture, flipped_texcoord);\n" +
            "}\n" +
            "";
    }

	public class AttributeInfo
	{
		public String name = "";
		public int address = -1;
		public int size = 0;
		public ActiveAttribType type;
	}

	public class UniformInfo
	{
		public String name = "";
		public int address = -1;
		public int size = 0;
		public ActiveUniformType type;
	}

	public class CShaderProgram
	{
		public int ProgramID = -1;
		public int VShaderID = -1;
		public int FShaderID = -1;
		public int AttributeCount = 0;
		public int UniformCount = 0;
		static string path = @"Shaders";

		public Dictionary<String, AttributeInfo> Attributes = new Dictionary<string, AttributeInfo>();
		public Dictionary<String, UniformInfo> Uniforms = new Dictionary<string, UniformInfo>();
		public Dictionary<String, uint> Buffers = new Dictionary<string, uint>();

		public CShaderProgram ()
		{
			ProgramID = GL.CreateProgram();
		}

		public static string LoadShaderPointer(string name)
		{
			if(!CCore.GetCore().Renderer.shaders.ContainsKey(name))
				CCore.GetCore().Renderer.shaders.Add(name, new CShaderProgram("vs_"+name+".glsl", "fs_"+name+".glsl", true, true));
			return name;
		}

		public CShaderProgram(String vshader, String fshader, bool vFromFile = false, bool fFromFile = false)
		{
			CLog.WriteLine ("Initializing shaders...");
			ProgramID = GL.CreateProgram();

			if (fFromFile)
			{	
				LoadShaderFromFile(fshader, ShaderType.FragmentShader);
			}
			else
			{
				LoadShaderFromString(fshader, ShaderType.FragmentShader);
			}

            if(vFromFile)
            {
                LoadShaderFromFile(vshader, ShaderType.VertexShader);
            }
            else
            {
                LoadShaderFromString(vshader, ShaderType.VertexShader);
            }

			Link();
			GenBuffers();

			CLog.WriteLine ("Shaders compiled!");
		}

		void loadShader(string code, ShaderType type, out int address)
		{
			address = GL.CreateShader (type);
			GL.ShaderSource (address, code);
			GL.CompileShader (address);
			GL.AttachShader (ProgramID, address);
            string t = GL.GetShaderInfoLog(address);
            if(t != "")
			CLog.WriteLine (t);
		}

		/// <summary>
		/// Loads the shader from string.
		/// </summary>
		/// <param name="code">Code.</param>
		/// <param name="type">Type.</param>
		public void LoadShaderFromString(String code, ShaderType type)
		{
			if (type == ShaderType.VertexShader)
			{
				loadShader(code, type, out VShaderID);
			}
			else if (type == ShaderType.FragmentShader)
			{
				loadShader(code, type, out FShaderID);
			}
		}

		/// <summary>
		/// Loads the shader from file.
		/// </summary>
		/// <param name="filename">Filename.</param>
		/// <param name="type">Type.</param>
		public void LoadShaderFromFile(String filename, ShaderType type)
		{
            try
            {
                using (StreamReader sr = new StreamReader(Path.Combine(path, filename)))
                {
                    if (type == ShaderType.VertexShader)
                    {
                        loadShader(sr.ReadToEnd(), type, out VShaderID);
                    }
                    else if (type == ShaderType.FragmentShader)
                    {
                        loadShader(sr.ReadToEnd(), type, out FShaderID);
                    }
                }
            }
            catch
            {
                CLog.WriteLine(string.Format("Shader '{0}' not found! Using fallback shader...", filename));
                if(type == ShaderType.VertexShader)
                {
                    loadShader(DefaultShader.vs_fallback, type, out VShaderID);
                }
                else if(type == ShaderType.FragmentShader)
                {
                    loadShader(DefaultShader.fs_fallback, type, out FShaderID);
                }
            }
		}

		/// <summary>
		/// Link this instance.
		/// </summary>
		public void Link()
        {
			GL.LinkProgram (ProgramID);
			GL.UseProgram (ProgramID);
 
            CLog.WriteLine(GL.GetProgramInfoLog(ProgramID));
 
            GL.GetProgram(ProgramID, ProgramParameter.ActiveAttributes, out AttributeCount);
            GL.GetProgram(ProgramID, ProgramParameter.ActiveUniforms, out UniformCount);
 
            for (int i = 0; i < AttributeCount; i++)
            {
                AttributeInfo info = new AttributeInfo();
                int length = 0;
 
                StringBuilder name = new StringBuilder();
 
                GL.GetActiveAttrib(ProgramID, i, 256, out length, out info.size, out info.type, name);
 
                info.name = name.ToString();
                info.address = GL.GetAttribLocation(ProgramID, info.name);
                Attributes.Add(name.ToString(), info);
            }
 
            for (int i = 0; i < UniformCount; i++)
            {
                UniformInfo info = new UniformInfo();
                int length = 0;
 
                StringBuilder name = new StringBuilder();
 
                GL.GetActiveUniform(ProgramID, i, 256, out length, out info.size, out info.type, name);
 
                info.name = name.ToString();
                Uniforms.Add(name.ToString(), info);
                info.address = GL.GetUniformLocation(ProgramID, info.name);
            }
        }

		/// <summary>
		/// Generates the buffers.
		/// </summary>
		public void GenBuffers()
		{
			for (int i = 0; i < Attributes.Count; i++)
			{
				uint buffer = 0;
				GL.GenBuffers(1, out buffer);

				Buffers.Add(Attributes.Values.ElementAt(i).name, buffer);
			}

			for (int i = 0; i < Uniforms.Count; i++)
			{
				uint buffer = 0;
				GL.GenBuffers(1, out buffer);

				Buffers.Add(Uniforms.Values.ElementAt(i).name, buffer);
			}
		}

		/// <summary>
		/// Enables the vertex attrib arrays.
		/// </summary>
		public void EnableVertexAttribArrays()
		{
			for (int i = 0; i < Attributes.Count; i++)
			{
				GL.EnableVertexAttribArray(Attributes.Values.ElementAt(i).address);
			}
		}

		/// <summary>
		/// Disables the vertex attrib arrays.
		/// </summary>
		public void DisableVertexAttribArrays()
		{
			for (int i = 0; i < Attributes.Count; i++)
			{
				GL.DisableVertexAttribArray(Attributes.Values.ElementAt(i).address);
			}
		}

		public int GetAttribute(string name)
		{
			if (Attributes.ContainsKey(name))
			{
				return Attributes[name].address;
			}
			else
			{
				return -1;
			}
		}

		public int GetUniform(string name)
		{
			if (Uniforms.ContainsKey(name))
			{
				return Uniforms[name].address;
			}
			else
			{
				return -1;
			}
		}

		public uint GetBuffer(string name)
		{
			if (Buffers.ContainsKey(name))
			{
				return Buffers[name];
			}
			else
			{
				return 0;
			}
		}
	}
}

