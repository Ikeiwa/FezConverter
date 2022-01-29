using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.IO;
using System.Globalization;
using Ara3D;
using System.Drawing;

namespace FezConverter
{

    public static class AraExtensions
    {
        public static string ToString(this Vector2 vec, string prefix)
        {
            return prefix + ' ' + vec.X + ' ' + vec.Y + '\n';
        }

        public static string ToString(this Vector3 vec, string prefix)
        {
            return prefix + ' ' + vec.X + ' ' + vec.Y + ' ' + vec.Z + '\n';
        }

        public static Vector3 Cross(Vector3 v1, Vector3 v2)
        {
            float x, y, z;
            x = v1.Y * v2.Z - v2.Y * v1.Z;
            y = (v1.X * v2.Z - v2.X * v1.Z) * -1;
            z = v1.X * v2.Y - v2.X * v1.Y;

            var rtnvector = new Vector3(x, y, z);
            rtnvector.Normalize();
            return rtnvector;
        }

        public static Vector3 Mul(Vector3 v, Quaternion q)
        {
            Vector3 u = new Vector3(q.X, q.Y, q.Z);
            float s = q.W;

            return
                    2.0f * Vector3.Dot(u, v) * u
                    + (s * s - Vector3.Dot(u, u)) * v
                    + 2.0f * s * Cross(u, v);
        }
    }
    

    public struct Vertex
    {
        public Vector3 Position;
        public Vector3 Normal;
        public Vector2 TextureCoord;

        public void TransformPos(Vector3 pos, Quaternion rot, Vector3 scale)
        {
            Position *= scale;
            Position = Position.Transform(rot);
            Position += pos;
        }
    }

    public struct Face
    {
        public int vertex1;
        public int vertex2;
        public int vertex3;

        public override string ToString()
        {
            return "f " + vertex1 + '/' + vertex1 + '/' + vertex1 + ' ' +
                          vertex2 + '/' + vertex2 + '/' + vertex2 + ' ' +
                          vertex3 + '/' + vertex3 + '/' + vertex3 + '\n';
        }
    }

    class Program
    {
        static Quaternion[] orientations = new Quaternion[4]
        {
            Quaternion.CreateFromYawPitchRoll(Constants.Pi,0,0),
            Quaternion.CreateFromYawPitchRoll(-Constants.HalfPi,0,0),
            Quaternion.CreateFromYawPitchRoll(0,0,0),
            Quaternion.CreateFromYawPitchRoll(Constants.HalfPi,0,0)
        };

        private const float TEX_EPSILON = 0.005f;

        static NumberFormatInfo fmt = new NumberFormatInfo { NegativeSign = "-", NumberDecimalSeparator = "." };

        static void ConvertArtObject(string path, XmlDocument modelDoc)
        {
            Console.WriteLine("Converting ArtObject");

            XmlNode sizeNode = modelDoc.SelectSingleNode("/ArtObject/Size/Vector3");
            Vector3 offset = new Vector3
            (
                float.Parse(sizeNode.Attributes["x"].Value, fmt),
                float.Parse(sizeNode.Attributes["y"].Value, fmt),
                float.Parse(sizeNode.Attributes["z"].Value, fmt)
            );
            offset = -offset / 2.0f;



            string objData = ConvertArtObjectModel(modelDoc.SelectSingleNode("/ArtObject"), offset, out string objName);

            File.WriteAllText(path.Replace(".xml", ".obj"), objData.Replace(",", "."));

            string mtlData = "newmtl " + objName + "\n";
            mtlData += "Ns 0.000000\nKa 1.000000 1.000000 1.000000\nKd 1.000000 1.000000 1.000000\nKs 0.000000 0.000000 0.000000\nKe 0.000000 0.000000 0.000000\nNi 1.000000\nd 1.000000\nillum 2\nmap_kd ";
            mtlData += Path.GetFileNameWithoutExtension(path) + ".png";

            File.WriteAllText(path.Replace(".xml", ".mtl"), mtlData.Replace(",", "."));
        }

        static string LoadArtObject(string path, string name,Vector3 offset, Vector3 position, Quaternion rotation, Vector3 scale, ref int vertexOffset, out string mtlData)
        {
            path = Path.Combine(path, "art objects", name.ToLower().Replace(" ", "_")+".xml");
            string objData = "";

            //Console.WriteLine(path);

            XmlDocument modelDoc = new XmlDocument();
            try { modelDoc.Load(path); }
            catch (FileNotFoundException)
            {
                Console.WriteLine("Not a valid XML");
                mtlData = "";
                return objData;
            }

            XmlNode sizeNode = modelDoc.SelectSingleNode("/ArtObject/Size/Vector3");
            offset = -offset / 2.0f - new Vector3(0.5f);

            objData = ConvertArtObjectModel(modelDoc.SelectSingleNode("/ArtObject"), offset, position, rotation, scale, ref vertexOffset, out string objName).Replace(",", ".");
            objData += "\n\n";

            mtlData = "newmtl " + objName + "\n";
            mtlData += "Ns 0.000000\nKa 1.000000 1.000000 1.000000\nKd 1.000000 1.000000 1.000000\nKs 0.000000 0.000000 0.000000\nKe 0.000000 0.000000 0.000000\nNi 1.000000\nd 1.000000\nillum 2\nmap_kd ";
            mtlData += Path.GetFileNameWithoutExtension(path) + ".png";
            mtlData += "\n\n";
            mtlData = mtlData.Replace(",", ".");

            return objData;
        }

        static string ConvertArtObjectModel(XmlNode meshNode, Vector3 offset, out string objName)
        {
            int vertexOffset = 0;
            return ConvertArtObjectModel(meshNode, offset, Vector3.Zero, Quaternion.Identity, Vector3.One, ref vertexOffset, out objName);
        }

        static string ConvertArtObjectModel(XmlNode meshNode, Vector3 offset, Vector3 position, Quaternion rotation, Vector3 scale, ref int vertexOffset, out string objName)
        {
            string objData = "";

            XmlNodeList verticesXml = meshNode.SelectNodes("//VertexPositionNormalTextureInstance");
            List<Vertex> vertices = new List<Vertex>();

            foreach (XmlNode node in verticesXml)
            {
                XmlNode posNode = node.SelectSingleNode("./Position/Vector3");
                Vector3 pos = new Vector3
                (
                    float.Parse(posNode.Attributes["x"].Value, fmt),
                    float.Parse(posNode.Attributes["y"].Value, fmt),
                    float.Parse(posNode.Attributes["z"].Value, fmt)
                );
                pos = AraExtensions.Mul(pos,rotation);
                pos *= scale;
                pos += position + offset;

                int faceSide = int.Parse(node.SelectSingleNode("./Normal").InnerText, fmt);
                Vector3 normal;

                switch (faceSide)
                {
                    case 0:
                        normal = new Vector3(-1, 0, 0);
                        break;
                    case 1:
                        normal = new Vector3(0, -1, 0);
                        break;
                    case 2:
                        normal = new Vector3(0, 0, -1);
                        break;
                    case 3:
                        normal = new Vector3(1, 0, 0);
                        break;
                    case 4:
                        normal = new Vector3(0, 1, 0);
                        break;
                    case 5:
                        normal = new Vector3(0, 0, 1);
                        break;
                    default:
                        normal = new Vector3(-1, 0, 0);
                        break;
                }

                XmlNode uvNode = node.SelectSingleNode("./TextureCoord/Vector2");
                Vector2 uv = new Vector2
                (
                    float.Parse(uvNode.Attributes["x"].Value, fmt),
                    1.0f - float.Parse(uvNode.Attributes["y"].Value, fmt)
                );

                vertices.Add(new Vertex { Position = pos, Normal = normal, TextureCoord = uv });
            }
            

            XmlNodeList facesXmlList = meshNode.SelectNodes("//Indices/Index");
            XmlNode[] facesXml = new List<XmlNode>(Shim<XmlNode>(facesXmlList)).ToArray();
            List<Face> faces = new List<Face>();

            for (int i = 0; i < facesXml.Length; i += 3)
            {
                Face face = new Face
                {
                    vertex1 = int.Parse(facesXml[i].InnerText) + 1 + vertexOffset,
                    vertex2 = int.Parse(facesXml[i + 1].InnerText) + 1 + vertexOffset,
                    vertex3 = int.Parse(facesXml[i + 2].InnerText) + 1 + vertexOffset,
                };
                faces.Add(face);
            }

            vertexOffset += verticesXml.Count;

            objName = meshNode.Attributes["name"].Value.Replace(" ", "_");
            objData = "o " + objName + "\n";
            objData += "usemtl " + objName + "\n\n";

            for (int i = 0; i < vertices.Count; i++)
            {
                objData += vertices[i].Position.ToString("v");
            }
            objData += '\n';
            for (int i = 0; i < vertices.Count; i++)
            {
                objData += vertices[i].TextureCoord.ToString("vt");
            }
            objData += '\n';
            for (int i = 0; i < vertices.Count; i++)
            {
                objData += vertices[i].Normal.ToString("vn");
            }
            objData += '\n';
            for (int i = 0; i < faces.Count; i++)
            {
                objData += faces[i].ToString();
            }

            return objData;
        }


        static void ConvertTrileSet(string path, XmlDocument modelDoc)
        {
            Console.WriteLine("Converting TrileSet");

            string objData = "";

            XmlNodeList triles = modelDoc.SelectNodes("//Triles/TrileEntry/Trile");
            Console.WriteLine(triles.Count + " Triles");

            int vertexOffset = 0;

            foreach (XmlNode trile in triles)
            {
                objData += ConvertTrileModel(trile,ref vertexOffset, modelDoc.DocumentElement.Attributes["name"].Value, out string objName);
                objData += "\n\n";
            }

            File.WriteAllText(path.Replace(".xml", ".obj"), objData.Replace(",", "."));

            string mtlData = "newmtl " + modelDoc.DocumentElement.Attributes["name"].Value + "\n";
            mtlData += "Ns 0.000000\nKa 1.000000 1.000000 1.000000\nKd 1.000000 1.000000 1.000000\nKs 0.000000 0.000000 0.000000\nKe 0.000000 0.000000 0.000000\nNi 1.000000\nd 1.000000\nillum 2\nmap_kd ";
            mtlData += Path.GetFileNameWithoutExtension(path) + ".png";

            File.WriteAllText(path.Replace(".xml", ".mtl"), mtlData.Replace(",", "."));
        }

        static string LoadTrile(string path, XmlDocument tileset, string matName, string tileId, Vector3 offset, Vector3 position, int orientation, ref int vertexOffset)
        {
            XmlNode trileNode = tileset.SelectSingleNode("//TrileEntry[@key='"+ tileId + "']/Trile");

            string objData = ConvertTrileModel(trileNode, ref vertexOffset, matName, offset, position, orientation, out string objName);
            objData += "\n\n";
            return objData;
        }

        static string ConvertTrileModel(XmlNode meshNode, ref int vertexOffset, string matName, out string objName)
        {
            return ConvertTrileModel(meshNode, ref vertexOffset, matName, Vector3.Zero, Vector3.Zero, 0, out objName);
        }

        static string ConvertTrileModel(XmlNode meshNode,ref int vertexOffset, string matName, Vector3 offset, Vector3 position, int orientation, out string objName)
        {
            objName = meshNode.Attributes["name"].Value.Replace(" ", "_");
            //Console.WriteLine("Converting " + objName);

            string objData = "";

            /*XmlNode sizeNode = meshNode.SelectSingleNode("./Size/Vector3");
            Vector3 size = new Vector3
            (
                -float.Parse(sizeNode.Attributes["x"].Value, fmt) / 2f,
                -float.Parse(sizeNode.Attributes["y"].Value, fmt) / 2f,
                -float.Parse(sizeNode.Attributes["z"].Value, fmt) / 2f
            );

            XmlNode offsetNode = meshNode.SelectSingleNode("./Offset/Vector3");
            Vector3 offset = new Vector3
            (
                -float.Parse(offsetNode.Attributes["x"].Value, fmt),
                -float.Parse(offsetNode.Attributes["y"].Value, fmt),
                -float.Parse(offsetNode.Attributes["z"].Value, fmt)
            );*/

            XmlNode atlasOffsetNode = meshNode.SelectSingleNode("./AtlasOffset/Vector2");
            Vector2 atlasOffset = new Vector2
            (
                float.Parse(atlasOffsetNode.Attributes["x"].Value, fmt),
                float.Parse(atlasOffsetNode.Attributes["y"].Value, fmt)
            );
            //Console.WriteLine("Atlas Offset: " + atlasOffset.X + " " + atlasOffset.Y);

            XmlNodeList verticesXml = meshNode.SelectNodes("./Geometry/ShaderInstancedIndexedPrimitives/Vertices/VertexPositionNormalTextureInstance");
            List<Vertex> vertices = new List<Vertex>();
            //Console.WriteLine("Vertex Offset: " + vertexOffset);
            //Console.WriteLine(verticesXml.Count + " vertices");

            foreach (XmlNode node in verticesXml)
            {
                XmlNode posNode = node.SelectSingleNode("./Position/Vector3");
                Vector3 pos = new Vector3
                (
                    float.Parse(posNode.Attributes["x"].Value, fmt),
                    float.Parse(posNode.Attributes["y"].Value, fmt),
                    float.Parse(posNode.Attributes["z"].Value, fmt)
                );
                pos = AraExtensions.Mul(pos,orientations[orientation]);
                pos += position + offset;


                int faceSide = int.Parse(node.SelectSingleNode("./Normal").InnerText, fmt);
                Vector3 normal;

                switch (faceSide)
                {
                    case 0:
                        normal = new Vector3(-1, 0, 0);
                        break;
                    case 1:
                        normal = new Vector3(0, -1, 0);
                        break;
                    case 2:
                        normal = new Vector3(0, 0, -1);
                        break;
                    case 3:
                        normal = new Vector3(1, 0, 0);
                        break;
                    case 4:
                        normal = new Vector3(0, 1, 0);
                        break;
                    case 5:
                        normal = new Vector3(0, 0, 1);
                        break;
                    default:
                        normal = new Vector3(-1, 0, 0);
                        break;
                }

                XmlNode uvNode = node.SelectSingleNode("./TextureCoord/Vector2");
                Vector2 uv = new Vector2
                (
                    float.Parse(uvNode.Attributes["x"].Value, fmt),
                    1.0f - (float.Parse(uvNode.Attributes["y"].Value, fmt))
                );

                vertices.Add(new Vertex { Position = pos, Normal = normal, TextureCoord = uv });
            }

            XmlNodeList facesXmlList = meshNode.SelectNodes("./Geometry/ShaderInstancedIndexedPrimitives/Indices/Index");
            XmlNode[] facesXml = new List<XmlNode>(Shim<XmlNode>(facesXmlList)).ToArray();
            List<Face> faces = new List<Face>();
            //Console.WriteLine(facesXmlList.Count + " faces");

            for (int i = 0; i < facesXml.Length; i += 3)
            {
                Face face = new Face
                {
                    vertex1 = int.Parse(facesXml[i].InnerText) + 1 + vertexOffset,
                    vertex2 = int.Parse(facesXml[i + 1].InnerText) + 1 + vertexOffset,
                    vertex3 = int.Parse(facesXml[i + 2].InnerText) + 1 + vertexOffset,
                };
                faces.Add(face);
            }

            vertexOffset += verticesXml.Count;

            objData = "o " + objName + "-" + vertexOffset + "\n";
            objData += "usemtl " + matName + "\n\n";

            for (int i = 0; i < vertices.Count; i++)
            {
                objData += vertices[i].Position.ToString("v");
            }
            objData += '\n';
            for (int i = 0; i < vertices.Count; i++)
            {
                objData += vertices[i].TextureCoord.ToString("vt");
            }
            objData += '\n';
            for (int i = 0; i < vertices.Count; i++)
            {
                objData += vertices[i].Normal.ToString("vn");
            }
            objData += '\n';
            for (int i = 0; i < faces.Count; i++)
            {
                objData += faces[i].ToString();
            }

            return objData;
        }


        static string LoadBackgroundPlane(string path, XmlNode planeNode, Vector3 offset, Vector3 position, Quaternion rotation, Vector3 scale, ref int vertexOffset, out string mtlData)
        {
            string objName = planeNode.Attributes["textureName"].Value.ToLower().Replace(" ", "_");
            string imgPath = Path.Combine(path, "background planes", objName + ".png");

            Vector2 imgActualSize = Vector2.Zero;
            Vector2 imgSize = Vector2.Zero;
            Vector2 spriteScale = Vector2.One;

            bool animated = planeNode.Attributes["animated"].Value == "True";
            if (animated)
            {
                string xmlPath = Path.Combine(path, "background planes", objName + ".xml");

                imgPath = imgPath.Replace(".png", ".ani.png");

                if (File.Exists(xmlPath))
                {
                    XmlDocument animXml = new XmlDocument();
                    animXml.Load(xmlPath);
                    int width = int.Parse(animXml.DocumentElement.Attributes["width"].Value);
                    int height = int.Parse(animXml.DocumentElement.Attributes["height"].Value);
                    imgSize = new Vector2(width, height);

                    int spriteWidth = int.Parse(animXml.DocumentElement.Attributes["actualWidth"].Value);
                    int spriteHeight = int.Parse(animXml.DocumentElement.Attributes["actualHeight"].Value);
                    imgActualSize = new Vector2(spriteWidth, spriteHeight);

                    spriteScale = imgSize / imgActualSize;

                }
                else
                {
                    Console.WriteLine("Missing anim xml");
                    mtlData = "";
                    return "";
                }
            }
            else
            {
                //backgroundPlanePng = backgroundPlanePath.string() + bpName + ".png";

                Image img = Image.FromFile(imgPath);
                imgActualSize = new Vector2(img.Width, img.Height);
                imgSize = imgActualSize;
                img.Dispose();
            }

            mtlData = "newmtl " + objName + "\n";
            mtlData += "Ns 0.000000\nKa 1.000000 1.000000 1.000000\nKd 1.000000 1.000000 1.000000\nKs 0.000000 0.000000 0.000000\nKe 0.000000 0.000000 0.000000\nNi 1.000000\nd 1.000000\nillum 2\nmap_kd ";
            mtlData += Path.GetFileName(imgPath);
            mtlData += "\n\n";
            mtlData = mtlData.Replace(",", ".");


            Vector3 norm = Vector3.UnitZ.Transform(rotation);
            Vector3 extraOffset = new Vector3(-0.5f, -0.5f, -0.5f);
            Vector3 pos = position + offset + norm * 0.0005f + extraOffset;
            Vector3 realScale = scale * new Vector3(imgActualSize.X / 16.0f, imgActualSize.Y / 16.0f, 1.0f);

            string objData = "o " + objName + "-" + vertexOffset + "\n";
            objData += "usemtl " + objName + "\n\n";

            offset = -offset / 2.0f - new Vector3(0.5f);
            
            bool doubleSided = planeNode.Attributes["doubleSided"].Value == "True";
            bool billboard = planeNode.Attributes["billboard"].Value == "True";
            bool lightmap = planeNode.Attributes["lightMap"].Value == "True";
            bool pixelatedLightmap = planeNode.Attributes["pixelatedLightmap"].Value == "True";
            bool clampTexture = planeNode.Attributes["clampTexture"].Value == "True";

            Vertex[] vertices =
            {
                new Vertex{Normal = norm, Position = new Vector3( -0.5f,  0.5f,  0.0f ), TextureCoord = new Vector2( 0.0f + TEX_EPSILON, 1.0f - TEX_EPSILON )},
                new Vertex{Normal = norm, Position = new Vector3(  0.5f,  0.5f,  0.0f ), TextureCoord = new Vector2( 1.0f - TEX_EPSILON, 1.0f - TEX_EPSILON )},
                new Vertex{Normal = norm, Position = new Vector3(  0.5f, -0.5f,  0.0f ), TextureCoord = new Vector2( 1.0f - TEX_EPSILON, 0.0f + TEX_EPSILON )},
                new Vertex{Normal = norm, Position = new Vector3( -0.5f, -0.5f,  0.0f ), TextureCoord = new Vector2( 0.0f + TEX_EPSILON, 0.0f + TEX_EPSILON )}
            };

            Face[] faces =
            {
                new Face{vertex1 = 1+vertexOffset, vertex2 = 2+vertexOffset, vertex3 = 3+vertexOffset},
                new Face{vertex1 = 3+vertexOffset, vertex2 = 4+vertexOffset, vertex3 = 1+vertexOffset}
            };

            string posString = "";
            string normString = "";
            string texString = "";
            string triString = "";

            foreach (Vertex vertex in vertices)
            {
                vertex.TransformPos(pos,rotation,realScale);
                posString += vertex.Position.ToString("v");
                normString += vertex.Normal.ToString("vn");
                texString += vertex.TextureCoord.ToString("vt");
            }

            foreach (Face face in faces)
            {
                triString += face.ToString();
            }

            objData += $"{posString}\n{texString}\n{normString}\n{triString}";

            objData += "\n\n";

            vertexOffset += 4;

            return objData;
        }


        static void ConvertLevel(string path, XmlDocument modelDoc)
        {
            string mainPath = Path.GetDirectoryName(Path.GetDirectoryName(path));
            string trileSetName = modelDoc.DocumentElement.Attributes["trileSetName"].Value.ToLower();
            string trileSetPath = Path.Combine(mainPath, "trile sets", trileSetName + ".xml");

            string exportPath = path.Replace(".xml", "");
            Directory.CreateDirectory(exportPath);

            XmlDocument trileSetDoc = new XmlDocument();
            try { trileSetDoc.Load(trileSetPath); }
            catch (FileNotFoundException)
            {
                Console.WriteLine("Not a valid XML");
                return;
            }

            int vertexOffset = 0;
            string objData = "";
            string mtlData = "";

            XmlNode sizeNode = modelDoc.SelectSingleNode("/Level/Size/Vector3");
            Vector3 offset = new Vector3
            (
                float.Parse(sizeNode.Attributes["x"].Value, fmt),
                float.Parse(sizeNode.Attributes["y"].Value, fmt),
                float.Parse(sizeNode.Attributes["z"].Value, fmt)
            );

            File.Copy(trileSetPath.Replace(".xml", ".png"), Path.Combine(exportPath, Path.GetFileNameWithoutExtension(trileSetPath) + ".png"),true);

            int currentConvert = 1;
            int totalConverts = 1;

            {
                XmlNodeList triles = modelDoc.SelectNodes("/Level/Triles/Entry/TrileInstance");
                totalConverts = triles.Count;
                foreach (XmlNode trile in triles)
                {
                    Console.WriteLine(currentConvert + "/" + totalConverts + " Triles");
                    XmlNode posNode = trile.SelectSingleNode("./Position/Vector3");
                    Vector3 pos = new Vector3
                    (
                        float.Parse(posNode.Attributes["x"].Value, fmt),
                        float.Parse(posNode.Attributes["y"].Value, fmt),
                        float.Parse(posNode.Attributes["z"].Value, fmt)
                    );
                    
                    string trileId = trile.Attributes["trileId"].Value;

                    if(trileId != "-1")
                        objData += LoadTrile(trileSetPath, trileSetDoc, trileSetName, trileId,
                        -offset / 2f, pos, int.Parse(trile.Attributes["orientation"].Value, fmt), ref vertexOffset);
                    currentConvert++;
                }

                mtlData = "newmtl " + trileSetName + "\n";
                mtlData += "Ns 0.000000\nKa 1.000000 1.000000 1.000000\nKd 1.000000 1.000000 1.000000\nKs 0.000000 0.000000 0.000000\nKe 0.000000 0.000000 0.000000\nNi 1.000000\nd 1.000000\nillum 2\nmap_kd ";
                mtlData += Path.GetFileNameWithoutExtension(trileSetPath) + ".png";
                mtlData += "\n\n";
            }
            GC.Collect();

            {
                currentConvert = 1;
                XmlNodeList artObjects = modelDoc.SelectNodes("/Level/ArtObjects/Entry/ArtObjectInstance");
                totalConverts = artObjects.Count;
                foreach (XmlNode artObject in artObjects)
                {
                    Console.WriteLine(currentConvert + "/" + totalConverts + " Art objects");
                    string artPath = Path.Combine(mainPath, "art objects",
                        artObject.Attributes["name"].Value.ToLower().Replace(" ", "_") + ".xml");

                    File.Copy(artPath.Replace(".xml", ".png"),
                        Path.Combine(exportPath, Path.GetFileNameWithoutExtension(artPath) + ".png"), true);

                    XmlNode posNode = artObject.SelectSingleNode("./Position/Vector3");
                    Vector3 pos = new Vector3
                    (
                        float.Parse(posNode.Attributes["x"].Value, fmt),
                        float.Parse(posNode.Attributes["y"].Value, fmt),
                        float.Parse(posNode.Attributes["z"].Value, fmt)
                    );

                    XmlNode rotNode = artObject.SelectSingleNode("./Rotation/Quaternion");
                    Quaternion rot = new Quaternion
                    (
                        float.Parse(rotNode.Attributes["x"].Value, fmt),
                        float.Parse(rotNode.Attributes["y"].Value, fmt),
                        float.Parse(rotNode.Attributes["z"].Value, fmt),
                        float.Parse(rotNode.Attributes["w"].Value, fmt)
                    );

                    XmlNode scaleNode = artObject.SelectSingleNode("./Scale/Vector3");
                    Vector3 scale = new Vector3
                    (
                        float.Parse(scaleNode.Attributes["x"].Value, fmt),
                        float.Parse(scaleNode.Attributes["y"].Value, fmt),
                        float.Parse(scaleNode.Attributes["z"].Value, fmt)
                    );
                    objData += LoadArtObject(mainPath, artObject.Attributes["name"].Value, offset, pos, rot, scale,
                        ref vertexOffset, out string objMtlData);
                    mtlData += objMtlData;

                    currentConvert++;
                }
            }
            GC.Collect();

            {
                currentConvert = 1;
                XmlNodeList backgroundPlanes = modelDoc.SelectNodes("/Level/BackgroundPlanes/Entry/BackgroundPlane");
                totalConverts = backgroundPlanes.Count;
                foreach (XmlNode backgroundPlane in backgroundPlanes)
                {
                    Console.WriteLine(currentConvert + "/" + totalConverts + " Background planes");
                    string artPath = Path.Combine(mainPath, "background planes",
                        backgroundPlane.Attributes["textureName"].Value.ToLower().Replace(" ", "_") + ".png");

                    if (backgroundPlane.Attributes["animated"].Value == "True")
                        artPath = artPath.Replace(".png", ".ani.png");

                    File.Copy(artPath, Path.Combine(exportPath, Path.GetFileName(artPath)), true);

                    XmlNode posNode = backgroundPlane.SelectSingleNode("./Position/Vector3");
                    Vector3 pos = new Vector3
                    (
                        float.Parse(posNode.Attributes["x"].Value, fmt),
                        float.Parse(posNode.Attributes["y"].Value, fmt),
                        float.Parse(posNode.Attributes["z"].Value, fmt)
                    );

                    XmlNode rotNode = backgroundPlane.SelectSingleNode("./Rotation/Quaternion");
                    Quaternion rot = new Quaternion
                    (
                        float.Parse(rotNode.Attributes["x"].Value, fmt),
                        float.Parse(rotNode.Attributes["y"].Value, fmt),
                        float.Parse(rotNode.Attributes["z"].Value, fmt),
                        float.Parse(rotNode.Attributes["w"].Value, fmt)
                    );

                    XmlNode scaleNode = backgroundPlane.SelectSingleNode("./Scale/Vector3");
                    Vector3 scale = new Vector3
                    (
                        float.Parse(scaleNode.Attributes["x"].Value, fmt),
                        float.Parse(scaleNode.Attributes["y"].Value, fmt),
                        float.Parse(scaleNode.Attributes["z"].Value, fmt)
                    );
                    objData += LoadBackgroundPlane(mainPath, backgroundPlane, -offset / 2f, pos, rot, scale, ref vertexOffset,
                        out string objMtlData);
                    mtlData += objMtlData;

                    currentConvert++;
                }
            }
            GC.Collect();

            string objPath = Path.Combine(exportPath, Path.GetFileNameWithoutExtension(path)) + ".obj";
            string mtlPath = Path.Combine(exportPath, Path.GetFileNameWithoutExtension(path)) + ".mtl";

            File.WriteAllText(objPath, objData.Replace(",", "."));
            File.WriteAllText(mtlPath, mtlData.Replace(",", "."));
        }


        static void ExtractSprites(string path, XmlDocument spriteDoc)
        {
            XmlNodeList sprites = spriteDoc.SelectNodes("/AnimatedTexturePC/Frames/FramePC");
            Bitmap spriteSheet = new Bitmap(path.Replace(".xml", ".ani.png"));
            string spriteName = Path.GetFileNameWithoutExtension(path);
            string mainFolder = Directory.CreateDirectory(Path.Combine(Path.GetDirectoryName(path), spriteName + "-frames")).FullName;

            for (int i=0;i< sprites.Count;i++)
            {
                XmlNode rectNode = sprites[i].SelectSingleNode("./Rectangle");
                Rectangle rect = new Rectangle
                (
                    int.Parse(rectNode.Attributes["x"].Value, fmt),
                    int.Parse(rectNode.Attributes["y"].Value, fmt),
                    int.Parse(rectNode.Attributes["w"].Value, fmt),
                    int.Parse(rectNode.Attributes["h"].Value, fmt)
                );

                Image sprite = spriteSheet.Clone(rect, spriteSheet.PixelFormat);
                sprite.Save(Path.Combine(mainFolder, spriteName + "-" + i+".png"));
            }
        }


        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.ReadLine();
                return;
            }

            Console.WriteLine(args[0]);
            string path = args[0];

            XmlDocument modelDoc = new XmlDocument();
            try { modelDoc.Load(path);}
            catch (FileNotFoundException)
            {
                Console.WriteLine("Not a valid XML");
                return;
            }

            Console.WriteLine(modelDoc.DocumentElement.Name);

            switch (modelDoc.DocumentElement.Name)
            {
                case "ArtObject":
                    ConvertArtObject(path, modelDoc);
                    break;
                case "TrileSet":
                    ConvertTrileSet(path, modelDoc);
                    break;
                case "Level":
                    ConvertLevel(path, modelDoc);
                    break;
                case "AnimatedTexturePC":
                    ExtractSprites(path, modelDoc);
                    break;
                default:
                    Console.WriteLine("Unknown model format");
                    break;
            }

            Console.WriteLine("Finished !");
            Console.ReadLine();
        }

        public static IEnumerable<T> Shim<T>(IEnumerable enumerable)
        {
            foreach (object current in enumerable)
            {
                yield return (T)current;
            }
        }

        public static Image CropBitmap(Bitmap bitmap, int cropX, int cropY, int cropWidth, int cropHeight)
        {
            Rectangle rect = new Rectangle(cropX, cropY, cropWidth, cropHeight);
            return bitmap.Clone(rect, bitmap.PixelFormat);
        }
    }
}
