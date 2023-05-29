using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using UnityEngine;

using Python.Runtime;

using System.IO;

public class Program : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {

    }

    public void runrunrun()
    {
        Debug.Log("clicked");

        var PYTHON_HOME = Environment.ExpandEnvironmentVariables(@"C:\Users\Noh\anaconda3\envs\gestenv38");
        // Can't find a usable init.tcl 에러로 아래 코드 추가
        //Environment.SetEnvironmentVariable("PYTHONNET_PYDLL", @"C:\Users\Noh\anaconda3\envs\gestenv38\Python38.dll", EnvironmentVariableTarget.Process);
        Runtime.PythonDLL = @"C:\Users\Noh\anaconda3\envs\gestenv38\Python38.dll";
        PythonEngine.PythonHome = PYTHON_HOME;
        // 모듈 패키지 패스 설정.
        PythonEngine.PythonPath = string.Join(
            
            Path.PathSeparator.ToString(),
            new string[] {
                  PythonEngine.PythonPath,
                     Path.Combine(PYTHON_HOME, @"Lib\site-packages"),
                     @"C:\Users\Noh\anaconda3\envs\gestenv38\Lib",
                     @"C:\Users\Noh\anaconda3\envs\gestenv38\DLLs",
                     @"C:\Users\Noh\Unity\UnityGesticulator\Assets\gesticulator", // the root folder itself  under which demo package resides; demo package has demo.py module
                     @"C:\Users\Noh\Unity\UnityGesticulator\Assets\gesticulator\gesticulator",
                     @"C:\Users\Noh\Unity\UnityGesticulator\Assets\gesticulator\gesticulator\visualization",
            }
        );

        // Python 엔진 초기화
        PythonEngine.Initialize();
        // Global Interpreter Lock을 취득
        using (Py.GIL())
        {
            dynamic demo = Py.Import("demo.demo");   // It uses  PythonEngine.PythonPath 
            dynamic arg_model_file = demo.main(@"C:\Users\Noh\Unity\UnityGesticulator\Assets\gesticulator\demo\input\jeremy_howard.wav", @"C:\Users\Noh\Unity\UnityGesticulator\Assets\gesticulator\demo\input\jeremy_howard.txt");

        }    // using GIL( Py.GIL() )
        // python 환경을 종료한다.
        PythonEngine.Shutdown();
        Debug.Log("PythonEngine.Shutdown");
    }



    void Startabcd()
    {
        //  where.exe python으로 나온 anaconda 설치 경로를 설정

        //var PYTHON_HOME = Environment.ExpandEnvironmentVariables(@"C:\Users\Noh\AppData\Local\Programs\Python\Python38");
        var PYTHON_HOME = Environment.ExpandEnvironmentVariables(@"C:\Users\Noh\anaconda3\envs\gestenv38");
        //Runtime.PythonDLL = @"C:\Users\Noh\AppData\Local\Programs\Python\Python38\Python38.dll";
        Runtime.PythonDLL = @"C:\Users\Noh\anaconda3\envs\gestenv38\Python38.dll";
        // public static string? PythonDLL { get; set; }; set => something happens.


        // 환경 변수 설정
        //AddEnvPath(PYTHON_HOME, Path.Combine(PYTHON_HOME, @"Library\bin"));  // Add path to PATH
        // Python 홈 설정.
        PythonEngine.PythonHome = PYTHON_HOME;
        // 모듈 패키지 패스 설정.
        PythonEngine.PythonPath = string.Join(

            Path.PathSeparator.ToString(),
            new string[] {
                  PythonEngine.PythonPath,
                     Path.Combine(PYTHON_HOME, @"Lib\site-packages"),
                     @"Assets\gesticulator",  // the root folder itself  under which demo package resides; demo package has demo.py module
                      @"Assets\gesticulator\gesticulator",
                       @"Assets\gesticulator\gesticulator\visualization"
            }
        );
        // Python 엔진 초기화
        PythonEngine.Initialize();
        // Global Interpreter Lock을 취득
        using (Py.GIL())
        {

            //  you  need to set the Api Compatibility Level to .Net 4.x in your Player Settings, because you  need to use the dynamic keyword.

            dynamic pysys = Py.Import("sys");   // It uses  PythonEngine.PythonPath 
            dynamic pySysPath = pysys.path;
            string[] sysPathArray = (string[])pySysPath;    // About conversion: https://csharp.hotexamples.com/site/file?hash=0x7a3b7b993fab126a5a205be68df1c82bd87e4de081aa0f5ad36909b54f95e3d7&fullName=&project=pythonnet/pythonnet

            List<string> sysPath = ((string[])pySysPath).ToList<string>();
            //Console.WriteLine(pysys.path);
            //Console.WriteLine(pySysPath);
            // Console.WriteLine(sysPath); 

            // Since the List collection class implements the IEnumerable interface, we are allowed to use the foreach loop to iterate its content.

            // sysPath.ForEach(i => Console.Write("{0}\t", i));   // https://stackoverflow.com/questions/52927/console-writeline-and-generic-list

            //for (int i = 0; i < sysPathArray.Length; i++)
            //{
            //    Console.Write("{0}\t", sysPath[i]);
            //}

            Debug.Log("\nsys.path:\n");
            Array.ForEach(sysPathArray, element => Debug.LogFormat("{0}\t", element));

            //Console.WriteLine(sysPathArray);

            // All python objects should be declared as dynamic type: https://discoverdot.net/projects/pythonnet

            dynamic os = Py.Import("os");

            dynamic pycwd = os.getcwd();
            string cwd = (string)pycwd;

            Debug.LogFormat("\n\n initial os.cwd={0}", cwd);



            //os.chdir(@"D:\Dropbox\metaverse\ConsoleApp1\ConsoleApp1\Python\gesticulator\demo");
            //pycwd = os.getcwd();
            //cwd = (string)pycwd;

            //Console.WriteLine("\n\n new os.cwd={0}", cwd, "\n\n");


            //dynamic np = Py.Import("numpy");

            //dynamic mod = Py.Import("examples.calculator");


            // dynamic  pyresult =  mod.getStr();
            // string result = (string) pyresult;

            //string   result = (string)pyresult;  //  Microsoft.CSharp.RuntimeBinder.RuntimeBinderException:: Cannot convert type 'Python.Runtime.PyObject' to 'int'

            //Debug.LogFormat("\n pythion result:{0}", pyresult);   

            // Debug.LogFormat("\n pythion result:{0}", result);



            dynamic demo = Py.Import("demo.demo");   // It uses  PythonEngine.PythonPath 
            Debug.LogFormat("\n demo module:{0}\n", demo);


            dynamic arg_model_file = demo.main();

            //string strresult = (string) arg_model_file;         // https://github.com/pythonnet/pythonnet/issues/451
            // python dynamically typed: Yes
            // C# dynamically typed: No, strongly typed. It uses lots of overloads to
            // return the required type. =>  Moreover, C# 4.0 is dynamically  typed too:
            // https://pythondotnet.python.narkive.com/4SDbJ9lz/python-net-dynamic-types-of-returns-pyobject-from-the-runtime
            // https://csharpdoc.hotexamples.com/class/Python.Runtime/PyObject


            // passing array: https://stackoverflow.com/questions/64990129/how-to-pass-array-to-a-function-in-net-using-pythonnet:

            //Try initializing it like this;

            //using (Py.GIL())
            //{
            //    trendln = Py.Import("trendln");
            //    dynamic h = new float[] { 1F, 2F, 3F };
            //    int a, b = trendln.calc_support_resistance(h);
            //}

            //https://github.com/pythonnet/pythonnet/issues/484

            //using (Py.GIL())
            //{
            //    var scope = Py.CreateScope();
            //    scope.Exec(
            //         "a=[1, \"2\"]"
            //    );
            //    dynamic a = scope.Get("a");
            //    object cc = a[0];
            //    Console.WriteLine(cc.GetType()); //print PyObject
            //    Console.WriteLine(cc.GetType() == typeof(PyInt)); //print false
            //    Console.WriteLine(cc);
            //    scope.Dispose();
            //}


            // Net to Python type conversions summary: https://github.com/pythonnet/pythonnet/issues/623
            //https://zditect.com/code/python/using-pythonnet-to-interface-csharp-library.html


            //Console.WriteLine("\n args.model_file:{0}", strresult);
            Debug.LogFormat("\n HI. args.model_file:{0}\n\n", arg_model_file);

            // Calculator의 add함수를 호출
            //Console.WriteLine(f.add());
        }    // using GIL( Py.GIL() )
             // python 환경을 종료한다.
        PythonEngine.Shutdown();
        //Console.WriteLine("Press any key...");
        //Console.ReadKey();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
