using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace KERBALISM
{
	[KSPAddon(KSPAddon.Startup.EveryScene, true)]
	class DebugDrawer : MonoBehaviour
	{
		public abstract class ObjectDrawer
		{
			public int frames;

			public ObjectDrawer(int frames = 1)
			{
				this.frames = frames;
			}

			public abstract void Draw();
		}

		public class Point : ObjectDrawer
		{
			readonly Vector3 pos;
			readonly Color color;

			public Point(Vector3 pos, Color color, int frames = 10) : base(frames)
			{
				this.pos = pos;
				this.color = color;
			}

			public override void Draw()
			{
				DrawPoint(pos, color);
			}
		}

		public class Line : ObjectDrawer
		{
			readonly Vector3 start;
			readonly Vector3 end;
			readonly Color color;

			public Line(Vector3 start, Vector3 end, Color color, int frames = 10) : base(frames)
			{
				this.start = start;
				this.end = end;
				this.color = color;
			}

			public Line(Vector3 start, Vector3 direction, Color color, float length = 1f, int frames = 10) : base(frames)
			{
				this.start = start;
				this.end = start + (direction * length);
				this.color = color;
			}

			public override void Draw()
			{
				GLStart();
				DrawLine(start, end, color);
				GLEnd();
			}
		}

		public class Arrow : ObjectDrawer
		{
			readonly Vector3 start;
			readonly Vector3 end;
			readonly Vector3 arrowDirection;
			readonly Color color;

			public Arrow(Vector3 start, Vector3 end, Color color, float arrowLength = 0.25f, int frames = 10) : base(frames)
			{
				this.start = start;
				this.end = end;
				this.color = color;
				arrowDirection = (end - start).normalized * arrowLength;
			}

			public Arrow(Vector3 start, Vector3 direction, Color color, float length = 1f, float arrowLength = 0.25f, int frames = 10) : base(frames)
			{
				this.start = start;
				this.end = start + (direction.normalized * length);
				this.color = color;
				arrowDirection = direction.normalized * arrowLength;
			}

			public override void Draw()
			{
				DrawArrow(start, arrowDirection, color);
				GLStart();
				DrawLine(start + arrowDirection, end, color);
				GLEnd();
			}
		}

		public class Bounds : ObjectDrawer
		{
			UnityEngine.Bounds bounds;
			Color color;

			public Bounds(UnityEngine.Bounds bounds, Color color, int frames = 10) : base(frames)
			{
				this.bounds = bounds;
				this.color = color;
			}

			public override void Draw()
			{
				DrawBounds(bounds, color);
			}
		}

		public class Transform : ObjectDrawer
		{
			UnityEngine.Transform transform;

			public Transform(UnityEngine.Transform transform, int frames = 10) : base(frames)
			{
				this.transform = transform;
			}

			public override void Draw()
			{
				DrawTransform(transform);
			}
		}

		public class Cube : ObjectDrawer
		{
			UnityEngine.Transform transform;
			Vector3 center;
			Vector3 size;
			Color color;

			public Cube(UnityEngine.Transform transform, Vector3 center, Vector3 size, Color color, int frames = 10) : base(frames)
			{
				this.transform = transform;
				this.center = center;
				this.size = size;
				this.color = color;
			}

			public override void Draw()
			{
				DrawLocalCube(transform, size, color, center);
			}
		}

		private static readonly List<ObjectDrawer> drawObjects = new List<ObjectDrawer>();

		private static Material _material;

		private static int glDepth = 0;

		private static Material material
		{
			get
			{
				if (_material == null) _material = new Material(Shader.Find("Hidden/Internal-Colored"));
				return _material;
			}
		}

		private static Camera cam;

		private static void GetActiveCam()
		{
			if (HighLogic.LoadedSceneIsEditor)
				cam = EditorLogic.fetch?.editorCamera;
			else if (HighLogic.LoadedSceneIsFlight)
				cam = MapView.MapIsEnabled ? PlanetariumCamera.Camera : FlightCamera.fetch?.mainCamera;
			else
				cam = Camera.main;
		}

		[Conditional("DEBUG")]
		public static void Draw(ObjectDrawer objectDrawer)
		{
			drawObjects.Add(objectDrawer);
		}


		[Conditional("DEBUG")]
		private void Start()
		{
			DontDestroyOnLoad(this);
			GetActiveCam();
		}

		[Conditional("DEBUG")]
		public void Update()
		{
			GetActiveCam();
		}

		[Conditional("DEBUG")]
		public void OnRenderObject()
		{
			for (int i = drawObjects.Count - 1; i >= 0; i--)
			{
				ObjectDrawer obj = drawObjects[i];
				obj.frames--;
				try
				{
					obj.Draw();
				}
				catch (Exception)
				{
					drawObjects.RemoveAt(i);
				}
				
				if (obj.frames <= 0)
				{
					drawObjects.RemoveAt(i);
				}
			}
		}

		protected static void GLStart()
		{
			if (glDepth == 0)
			{
				GL.PushMatrix();
				material.SetPass(0);
				GL.LoadPixelMatrix();
				GL.Begin(GL.LINES);
			}
			glDepth++;
		}

		protected static void GLEnd()
		{
			glDepth--;

			if (glDepth == 0)
			{
				GL.End();
				GL.PopMatrix();
			}
		}

		private static Vector3 Tangent(Vector3 normal)
		{
			Vector3 tangent = Vector3.Cross(normal, Vector3.right);
			if (tangent.sqrMagnitude <= float.Epsilon)
				tangent = Vector3.Cross(normal, Vector3.up);
			return tangent;
		}

		private static void DrawLine(Vector3 origin, Vector3 destination, Color color)
		{
			Vector3 screenPoint1 = cam.WorldToScreenPoint(origin);
			Vector3 screenPoint2 = cam.WorldToScreenPoint(destination);

			GL.Color(color);
			GL.Vertex3(screenPoint1.x, screenPoint1.y, 0);
			GL.Vertex3(screenPoint2.x, screenPoint2.y, 0);
		}

		private static void DrawRay(Vector3 origin, Vector3 direction, Color color)
		{
			Vector3 screenPoint1 = cam.WorldToScreenPoint(origin);
			Vector3 screenPoint2 = cam.WorldToScreenPoint(origin + direction);

			GL.Color(color);
			GL.Vertex3(screenPoint1.x, screenPoint1.y, 0);
			GL.Vertex3(screenPoint2.x, screenPoint2.y, 0);
		}

		private static void DrawTransform(UnityEngine.Transform t, float scale = 1.0f)
		{
			GLStart();

			DrawRay(t.position, t.up * scale, Color.green);
			DrawRay(t.position, t.right * scale, Color.red);
			DrawRay(t.position, t.forward * scale, Color.blue);

			GLEnd();
		}
		private static void DrawPoint(Vector3 position, Color color, float scale = 1.0f)
		{
			GLStart();
			GL.Color(color);

			DrawRay(position + Vector3.up * (scale * 0.5f), -Vector3.up * scale, color);
			DrawRay(position + Vector3.right * (scale * 0.5f), -Vector3.right * scale, color);
			DrawRay(position + Vector3.forward * (scale * 0.5f), -Vector3.forward * scale, color);

			GLEnd();
		}

		private static void DrawArrow(Vector3 position, Vector3 direction, Color color)
		{
			GLStart();
			GL.Color(color);

			DrawRay(position, direction, color);

			GLEnd();

			DrawCone(position + direction, -direction * 0.333f, color, 15);
		}

		private static void DrawCone(Vector3 position, Vector3 direction, Color color, float angle = 45)
		{
			float length = direction.magnitude;

			Vector3 forward = direction;
			Vector3 up = Tangent(forward).normalized;
			Vector3 right = Vector3.Cross(forward, up).normalized;

			float radius = length * Mathf.Tan(Mathf.Deg2Rad * angle);

			GLStart();
			GL.Color(color);

			DrawRay(position, direction + radius * up, color);
			DrawRay(position, direction - radius * up, color);
			DrawRay(position, direction + radius * right, color);
			DrawRay(position, direction - radius * right, color);

			GLEnd();

			DrawCircle(position + forward, direction, color, radius);
			DrawCircle(position + forward * 0.5f, direction, color, radius * 0.5f);
		}

		private static void DrawLocalMesh(UnityEngine.Transform transform, Mesh mesh, Color color)
		{
			if (mesh == null || mesh.triangles == null || mesh.vertices == null)
				return;
			int[] triangles = mesh.triangles;
			Vector3[] vertices = mesh.vertices;
			GLStart();
			GL.Color(color);

			for (int i = 0; i < triangles.Length; i += 3)
			{
				Vector3 p1 = transform.TransformPoint(vertices[triangles[i]]);
				Vector3 p2 = transform.TransformPoint(vertices[triangles[i + 1]]);
				Vector3 p3 = transform.TransformPoint(vertices[triangles[i + 2]]);
				DrawLine(p1, p2, color);
				DrawLine(p2, p3, color);
				DrawLine(p3, p1, color);
			}

			GLEnd();
		}

		private static void DrawBounds(UnityEngine.Bounds bounds, Color color)
		{
			Vector3 center = bounds.center;

			float x = bounds.extents.x;
			float y = bounds.extents.y;
			float z = bounds.extents.z;

			Vector3 topa = center + new Vector3(x, y, z);
			Vector3 topb = center + new Vector3(x, y, -z);
			Vector3 topc = center + new Vector3(-x, y, z);
			Vector3 topd = center + new Vector3(-x, y, -z);

			Vector3 bota = center + new Vector3(x, -y, z);
			Vector3 botb = center + new Vector3(x, -y, -z);
			Vector3 botc = center + new Vector3(-x, -y, z);
			Vector3 botd = center + new Vector3(-x, -y, -z);

			GLStart();
			GL.Color(color);

			// Top
			DrawLine(topa, topc, color);
			DrawLine(topa, topb, color);
			DrawLine(topc, topd, color);
			DrawLine(topb, topd, color);

			// Sides
			DrawLine(topa, bota, color);
			DrawLine(topb, botb, color);
			DrawLine(topc, botc, color);
			DrawLine(topd, botd, color);

			// Bottom
			DrawLine(bota, botc, color);
			DrawLine(bota, botb, color);
			DrawLine(botc, botd, color);
			DrawLine(botd, botb, color);

			GLEnd();
		}

		private static void DrawRectTransform(RectTransform rectTransform, Canvas canvas, Color color)
		{

			Vector3[] corners = new Vector3[4];
			Vector3[] screenCorners = new Vector3[2];

			rectTransform.GetWorldCorners(corners);

			if (canvas.renderMode == RenderMode.ScreenSpaceCamera || canvas.renderMode == RenderMode.WorldSpace)
			{
				screenCorners[0] = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, corners[1]);
				screenCorners[1] = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, corners[3]);
			}
			else
			{
				screenCorners[0] = RectTransformUtility.WorldToScreenPoint(null, corners[1]);
				screenCorners[1] = RectTransformUtility.WorldToScreenPoint(null, corners[3]);
			}

			GLStart();
			GL.Color(color);

			GL.Vertex3(screenCorners[0].x, screenCorners[0].y, 0);
			GL.Vertex3(screenCorners[0].x, screenCorners[1].y, 0);

			GL.Vertex3(screenCorners[0].x, screenCorners[1].y, 0);
			GL.Vertex3(screenCorners[1].x, screenCorners[1].y, 0);

			GL.Vertex3(screenCorners[1].x, screenCorners[1].y, 0);
			GL.Vertex3(screenCorners[1].x, screenCorners[0].y, 0);

			GL.Vertex3(screenCorners[1].x, screenCorners[0].y, 0);
			GL.Vertex3(screenCorners[0].x, screenCorners[0].y, 0);

			GLEnd();
		}


		private static void DrawLocalCube(UnityEngine.Transform transform, Vector3 size, Color color, Vector3 center = default(Vector3))
		{
			Vector3 topa = transform.TransformPoint(center + new Vector3(-size.x, size.y, -size.z) * 0.5f);
			Vector3 topb = transform.TransformPoint(center + new Vector3(size.x, size.y, -size.z) * 0.5f);

			Vector3 topc = transform.TransformPoint(center + new Vector3(size.x, size.y, size.z) * 0.5f);
			Vector3 topd = transform.TransformPoint(center + new Vector3(-size.x, size.y, size.z) * 0.5f);

			Vector3 bota = transform.TransformPoint(center + new Vector3(-size.x, -size.y, -size.z) * 0.5f);
			Vector3 botb = transform.TransformPoint(center + new Vector3(size.x, -size.y, -size.z) * 0.5f);

			Vector3 botc = transform.TransformPoint(center + new Vector3(size.x, -size.y, size.z) * 0.5f);
			Vector3 botd = transform.TransformPoint(center + new Vector3(-size.x, -size.y, size.z) * 0.5f);

			GLStart();
			GL.Color(color);

			//top
			DrawLine(topa, topb, color);
			DrawLine(topb, topc, color);
			DrawLine(topc, topd, color);
			DrawLine(topd, topa, color);

			//Sides
			DrawLine(topa, bota, color);
			DrawLine(topb, botb, color);
			DrawLine(topc, botc, color);
			DrawLine(topd, botd, color);

			//Bottom
			DrawLine(bota, botb, color);
			DrawLine(botb, botc, color);
			DrawLine(botc, botd, color);
			DrawLine(botd, bota, color);

			GLEnd();
		}

		private static void DrawCapsule(Vector3 start, Vector3 end, Color color, float radius = 1)
		{
			int segments = 18;
			float segmentsInv = 1f / segments;

			Vector3 up = (end - start).normalized * radius;
			Vector3 forward = Tangent(up).normalized * radius;
			Vector3 right = Vector3.Cross(up, forward).normalized * radius;

			float height = (start - end).magnitude;
			float sideLength = Mathf.Max(0, height * 0.5f - radius);
			Vector3 middle = (end + start) * 0.5f;

			start = middle + (start - middle).normalized * sideLength;
			end = middle + (end - middle).normalized * sideLength;

			//Radial circles
			DrawCircle(start, up, color, radius);
			DrawCircle(end, -up, color, radius);

			GLStart();
			GL.Color(color);

			//Side lines
			DrawLine(start + right, end + right, color);
			DrawLine(start - right, end - right, color);

			DrawLine(start + forward, end + forward, color);
			DrawLine(start - forward, end - forward, color);

			for (int i = 1; i <= segments; i++)
			{
				float stepFwd = i * segmentsInv;
				float stepBck = (i - 1) * segmentsInv;
				//Start endcap
				DrawLine(Vector3.Slerp(right, -up, stepFwd) + start, Vector3.Slerp(right, -up, stepBck) + start, color);
				DrawLine(Vector3.Slerp(-right, -up, stepFwd) + start, Vector3.Slerp(-right, -up, stepBck) + start, color);
				DrawLine(Vector3.Slerp(forward, -up, stepFwd) + start, Vector3.Slerp(forward, -up, stepBck) + start, color);
				DrawLine(Vector3.Slerp(-forward, -up, stepFwd) + start, Vector3.Slerp(-forward, -up, stepBck) + start, color);

				//End endcap
				DrawLine(Vector3.Slerp(right, up, stepFwd) + end, Vector3.Slerp(right, up, stepBck) + end, color);
				DrawLine(Vector3.Slerp(-right, up, stepFwd) + end, Vector3.Slerp(-right, up, stepBck) + end, color);
				DrawLine(Vector3.Slerp(forward, up, stepFwd) + end, Vector3.Slerp(forward, up, stepBck) + end, color);
				DrawLine(Vector3.Slerp(-forward, up, stepFwd) + end, Vector3.Slerp(-forward, up, stepBck) + end, color);
			}

			GLEnd();
		}

		private static void DrawCircle(Vector3 position, Vector3 up, Color color, float radius = 1.0f)
		{
			int segments = 36;
			float step = Mathf.Deg2Rad * 360f / segments;

			Vector3 upNormal = up.normalized * radius;
			Vector3 forwardNormal = Tangent(upNormal).normalized * radius;
			Vector3 rightNormal = Vector3.Cross(upNormal, forwardNormal).normalized * radius;

			Matrix4x4 matrix = new Matrix4x4();

			matrix[0] = rightNormal.x;
			matrix[1] = rightNormal.y;
			matrix[2] = rightNormal.z;

			matrix[4] = upNormal.x;
			matrix[5] = upNormal.y;
			matrix[6] = upNormal.z;

			matrix[8] = forwardNormal.x;
			matrix[9] = forwardNormal.y;
			matrix[10] = forwardNormal.z;

			Vector3 lastPoint = position + matrix.MultiplyPoint3x4(Vector3.right);

			GLStart();
			GL.Color(color);

			for (int i = 0; i <= segments; i++)
			{
				Vector3 nextPoint;
				var angle = i * step;
				nextPoint.x = Mathf.Cos(angle);
				nextPoint.z = Mathf.Sin(angle);
				nextPoint.y = 0;

				nextPoint = position + matrix.MultiplyPoint3x4(nextPoint);

				DrawLine(lastPoint, nextPoint, color);
				lastPoint = nextPoint;
			}
			GLEnd();
		}

		private static void DrawSphere(Vector3 position, Color color, float radius = 1.0f)
		{
			int segments = 36;
			float step = Mathf.Deg2Rad * 360f / segments;

			Vector3 x = new Vector3(position.x, position.y, position.z + radius);
			Vector3 y = new Vector3(position.x + radius, position.y, position.z);
			Vector3 z = new Vector3(position.x + radius, position.y, position.z);

			GLStart();
			GL.Color(color);

			for (int i = 1; i <= segments; i++)
			{
				float angle = step * i;
				Vector3 nextX = new Vector3(position.x, position.y + radius * Mathf.Sin(angle), position.z + radius * Mathf.Cos(angle));
				Vector3 nextY = new Vector3(position.x + radius * Mathf.Cos(angle), position.y, position.z + radius * Mathf.Sin(angle));
				Vector3 nextZ = new Vector3(position.x + radius * Mathf.Cos(angle), position.y + radius * Mathf.Sin(angle), position.z);

				DrawLine(x, nextX, color);
				DrawLine(y, nextY, color);
				DrawLine(z, nextZ, color);

				x = nextX;
				y = nextY;
				z = nextZ;
			}
			GLEnd();
		}

		private static void DrawCylinder(Vector3 start, Vector3 end, Color color, float radius = 1)
		{
			Vector3 up = (end - start).normalized * radius;
			Vector3 forward = Tangent(up);
			Vector3 right = Vector3.Cross(up, forward).normalized * radius;

			//Radial circles
			DrawCircle(start, up, color, radius);
			DrawCircle(end, -up, color, radius);
			DrawCircle((start + end) * 0.5f, up, color, radius);

			GLStart();
			GL.Color(color);

			//Sides
			DrawLine(start + right, end + right, color);
			DrawLine(start - right, end - right, color);

			DrawLine(start + forward, end + forward, color);
			DrawLine(start - forward, end - forward, color);

			//Top
			DrawLine(start - right, start + right, color);
			DrawLine(start - forward, start + forward, color);

			//Bottom
			DrawLine(end - right, end + right, color);
			DrawLine(end - forward, end + forward, color);
			GLEnd();
		}

	}
}
