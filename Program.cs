#define debug

using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Media;

namespace GluttonousSnake
{
    class Program
    {
        static void Main(string[] args)
        {
            Game game = new Game();
        }
    }

    class Game
    {
        //config
        const int panelLength = 10;
        const int panelHeight = 10;
        const char frameElem1 = '■';
        const char frameElem2 = '□';
        const char snakeElem = '●';
        const char foodElem = '★';
        bool usAI = false;
        const int init_snake_length = 3;
        const int init_react_space = 2;
        const int update_time_span = 200;
        const int food_score = 10;

        //panelData
        enum PanelDirection:byte
        {
            None,
            Up,
            Down,
            Left,
            Right
        }
        struct PanelData
        {
            public bool isBody,isFood;
        }
        struct Point
        {
            public int x;
            public int y;
            public Point(int x, int y)
            {
                this.x = x;
                this.y = y;
            }
            public static bool operator ==(Point a, Point b)
            {
                if (a.x == b.x && a.y == b.y)
                    return true;
                else
                    return false;
            }
            public static bool operator !=(Point a, Point b)
            {
                if (a == b)
                    return false;
                else
                    return true;
            }

            public override bool Equals(object o)
            {
                return o.GetHashCode() == x * 100 + y;
            }

            public override int GetHashCode()
            {
                return x * 100 + y;
            }

            public static Point operator +(Point a,Point b)
            {
                a.x += b.x;
                a.y += b.y;
                return a;
            }
    }

        PanelData[,] panel_datas = new PanelData[panelHeight,panelLength];
        //use empty_point to find avaliable food position
        HashSet<Point> empty_point = new HashSet<Point>();
        Point[] direction_vec = new Point[] { new Point(0,0),new Point(-1,0),new Point(1,0), new Point(0, -1), new Point(0, 1) };
        PanelDirection[] rev_direc = new PanelDirection[] { (PanelDirection)0, (PanelDirection)2, (PanelDirection)1,
            (PanelDirection)4, (PanelDirection)3 };
        //global Variable
        //startTime
        //nowTime
        int score = 0;
        ConsoleKeyInfo cki;
        readonly Random rand = new Random();
        PanelDirection now_direction;
        Point snake_head;
        Point snake_tail;
        Point food_point;
        //判断游戏是否应该逻辑结束
        bool end_flag = false;
        bool pause_flag = false;
        //控制队列，用来简化操作
        Queue<PanelDirection> input_que = new Queue<PanelDirection>();
        SoundPlayer soundp;
        
        enum Operation : byte
        {
            Pause,
            Up,
            Down,
            Left,
            Right,
            Esc,
            None
        }

        public Game()
        {
#if !debug
            Console.WriteLine("欢迎来到贪吃蛇！\n\nmade by Kun\n");
            Console.Write("是否开启AI：【y】开启AI/【n】手动控制");
            bool inputFlag = true;
            while (inputFlag)
            {
                cki = Console.ReadKey(true);
                Console.WriteLine();
                switch (cki.Key)
                {
                    case ConsoleKey.Y:
                        this.usAI = true;
                        inputFlag = false;
                        break;
                    case ConsoleKey.N:
                        this.usAI = false;
                        inputFlag = false;
                        break;
                    default:
                        Console.Write("请输入【y】或【n】");
                        break;
                }
            }
            if (!this.usAI)
            {
                Console.WriteLine("【游戏规则】\n - 利用方向键↑ ↓ ← → 或者 w a s d 控制移动\n - p键暂停\\恢复游戏，Esc键退出游戏" +
                    "\n - 蛇占满屏幕即为胜利");
            }
            Console.WriteLine("按任意键游戏开始");
            Console.ReadKey();
#endif
            this.Initial_window();
            this.Initial_panelData();
            this.Initial_snake();
            this.Run();
        }

        void Initial_window()
        {
            Console.SetWindowSize(1, 1);
            Console.SetBufferSize((panelLength+2)*2+1, panelHeight+3);
            Console.SetWindowSize((panelLength+2)*2+1, panelHeight+3);
            Console.CursorVisible = false;

            //disable resize window
            BanConSoleResizeTool bnt = new BanConSoleResizeTool();

            //load sound
            soundp = new SoundPlayer();
            soundp.SoundLocation = "sound.wav";
            soundp.Load();
        }

        void Initial_panelData()
        {
            for(int i = 0; i < panelHeight; i++)
            {
                for(int j = 0; j < panelLength; j++)
                {
                    Point t;
                    t.x = i;
                    t.y = j;
                    empty_point.Add(t);
                }
            }
        }
        void Initial_snake()
        {
            int min_legal_x_pos = init_snake_length + init_react_space;
            int max_legal_x_pos = panelHeight - min_legal_x_pos;
            int min_legal_y_pos = init_snake_length + init_react_space;
            int max_legal_y_pos = panelLength - min_legal_x_pos;
            this.now_direction = (PanelDirection)rand.Next(1, 5);
            this.snake_head.x = rand.Next(min_legal_x_pos, max_legal_x_pos);
            this.snake_head.y = rand.Next(min_legal_y_pos, max_legal_y_pos);
            this.panel_datas[this.snake_head.x, this.snake_head.y].isBody = true;
            empty_point.Remove(this.snake_head);

            Point pre = this.snake_head;
            for (int i = 0; i < init_snake_length-1; i++)
            {
                //find previous snake block
                //set is body = true
                //set pd = now_direction
                Point t;
                t = pre + direction_vec[(int)rev_direc[(int)now_direction]];
                empty_point.Remove(t);
                this.panel_datas[t.x, t.y].isBody = true;
                this.input_que.Enqueue(this.now_direction);
                pre.x = t.x;
                pre.y = t.y;
            }
            this.snake_tail = pre;

            food_point = GenerateFood();
            this.panel_datas[food_point.x, food_point.y].isFood = true;
        }

        //generate food
        //不是一个高效的的用法
        //但是在小数据中可以用
        Point GenerateFood()
        {
            return this.empty_point.ElementAt(rand.Next(this.empty_point.Count));
        }
        // draw game by panelData
        public void DrawGame()
        {
            //Console.Clear();
            Console.WriteLine('\r');
            StringBuilder panel_string = new StringBuilder();
            //输出分数
            for(int i = 0; i < ((panelLength+2)*2-14)/2; i++)
            {
                panel_string.Append(" ");
            }
            panel_string.Append(string.Format("SCORE : {0:00000}\n", this.score));
            for (int i = 0; i < panelHeight + 2; i++)
            {
                for (int j = 0; j < panelLength + 2; j++)
                {
                    if (i == 0 || i == panelHeight+1)
                    {
                        panel_string.Append(frameElem1);
                    }
                    else
                    {
                        if (j == 0 || j == panelLength + 1)
                        {
                            panel_string.Append(frameElem1);
                        }
                        else
                        {
                            if (this.panel_datas[i-1, j - 1].isBody)
                                panel_string.Append(snakeElem);
                            else if (this.panel_datas[i-1, j - 1].isFood)
                                panel_string.Append(foodElem);
                            else
                                panel_string.Append(frameElem2);

                        }
                    }
                }
                if(i!=panelHeight+1) panel_string.Append("\n");
            }
            Console.Write(panel_string.ToString());   
        }

        //get next direction
        Operation GetNextDirection()
        {
            if (this.usAI)
            {
                return Operation.Esc;
            }
            else
            {
                switch (cki.Key)
                {
                    case ConsoleKey.UpArrow:
                    case ConsoleKey.W:
                        return Operation.Up;
                    case ConsoleKey.DownArrow:
                    case ConsoleKey.S:
                        return Operation.Down;
                    case ConsoleKey.LeftArrow:
                    case ConsoleKey.A:
                        return Operation.Left;
                    case ConsoleKey.RightArrow:
                    case ConsoleKey.D:
                        return Operation.Right;
                    case ConsoleKey.P:
                        return Operation.Pause;
                    case ConsoleKey.Escape:
                        return Operation.Esc;
                    default:
                        return Operation.None;
                }
            }
        }
        public void Run()
        {
            if (!this.usAI)
            {
                Thread t = new Thread(new ThreadStart(() => { while (true) { this.cki = Console.ReadKey(true); } }));
                t.IsBackground = true;
                t.Start();
            }

            //一个时间变量，用来更新时间，使游戏每x刻后更新一次。
            DateTime nt = DateTime.Now;
            //统计时间间隔
            int tsp;
            //暂停检测
            bool pause_has_change = true;
            bool draw_pause_flag = false;
            do
            {
                Operation t = GetNextDirection();
                PanelDirection instant_input_direction = this.now_direction;
                if (t == Operation.Up || t == Operation.Down || t == Operation.Left || t == Operation.Right)
                {
                    instant_input_direction = (PanelDirection)t;
                    pause_has_change = true;
                    draw_pause_flag = false;
                }
                else if (t == Operation.Esc)
                {
                    Console.Clear();
                    DrawGame();
                    Draw_Hit("End!");
                    Thread.Sleep(2000);
                    Environment.Exit(0);
                }
                ///<summary>
                ///暂停功能
                ///希望P键暂停，再按P键恢复，同时暂停时间，其他毫无影响
                /// </summary>
                else if (t == Operation.Pause)
                {
                    //检查是刚按下的暂停键，还是陈旧的暂停键？
                    if (pause_has_change)
                    {
                        pause_has_change = false;
                        //已经暂停了
                        if (this.pause_flag)
                        {
                            //解除暂停
                            draw_pause_flag = false;
                        }
                        else
                        {
                            //暂停
                            if (!draw_pause_flag)
                            {
                                Draw_Hit("Pause!");
                                draw_pause_flag = true;
                            }
                            continue;
                        }

                    }
                    else
                    {
                        continue;
                    }

                }
                tsp = Convert.ToInt32((DateTime.Now - nt).TotalMilliseconds);
                if (tsp > update_time_span)
                {
                    if (this.now_direction != this.rev_direc[(int)instant_input_direction])
                            this.now_direction = instant_input_direction;
                    this.input_que.Enqueue(this.now_direction);
                    nt = DateTime.Now;

                    Point next_snake_head = this.snake_head + this.direction_vec[(int)this.now_direction];

                    //检查蛇头是否超出范围
                    //为什么不把蛇身相撞检查在这里做？因为食物蛇身增加与头尾相撞问题
                    if (next_snake_head.x < 0 || next_snake_head.y < 0 || next_snake_head.x >= panelHeight || next_snake_head.y >= panelLength)
                    {
                        end_flag = true;
                        break;
                    }
                    //检查蛇头下一位置是否为食物
                    bool food_flag = this.panel_datas[next_snake_head.x, next_snake_head.y].isFood;
                    if (!food_flag)
                    {
                        //蛇尾前进
                        PanelDirection td = this.input_que.Dequeue();
                        this.panel_datas[this.snake_tail.x, this.snake_tail.y].isBody = false;
                        this.empty_point.Add(snake_tail);
                        this.snake_tail += this.direction_vec[(int)td];
                        //检查前方是否为蛇身
                        if (this.panel_datas[next_snake_head.x, next_snake_head.y].isBody)
                        {
                            end_flag = true;
                            break;
                        }
                        else
                        {
                            //蛇头前进
                            this.panel_datas[next_snake_head.x, next_snake_head.y].isBody = true;
                            this.snake_head = next_snake_head;
                            this.empty_point.Remove(snake_head);
                        }
                    }
                    else
                    {
                        //有食物，蛇头前进，蛇尾不变，不出队列
                        this.panel_datas[next_snake_head.x, next_snake_head.y].isBody = true;
                        this.snake_head = next_snake_head;
                        this.empty_point.Remove(snake_head);

                        //清空食物并生成新食物
                        this.panel_datas[next_snake_head.x, next_snake_head.y].isFood = false;
                        Point new_foold = GenerateFood();
                        this.panel_datas[new_foold.x, new_foold.y].isFood = true;

                        //加分，播放声音
                        this.score += food_score;
                        this.soundp.Play();
                    }
                    this.DrawGame();
                }

            } while (!end_flag);
            if (end_flag)
            {
                if (this.empty_point.Count() == 0)
                {
                    Draw_Hit("Success!");
                }
                else
                {
                    Draw_Hit("Fail!");
                }
                Console.ReadKey();
            }
        }
        //绘制提示符
        private void Draw_Hit(string msg)
        {
            int msg_count = msg.Length+2;
            int start_x = panelLength + 2 - msg_count;
            if (start_x % 2 != 0)
            {
                start_x += 1;
            }
            int start_y = panelHeight/2;
            Console.SetCursorPosition(start_x, start_y);
            StringBuilder st = new StringBuilder();
            StringBuilder st1 = new StringBuilder();
            StringBuilder st2 = new StringBuilder();
            for (int i = 0; i < msg_count; i++)
            {
                st.Append("##");
            }
            Console.Write(st);
            
            st1.Append("##");
            st2.Append("##");
            for(int i = 0; i < msg.Length; i++)
            {
                st1.Append(" ");
                st1.Append(msg[i]);
                st2.Append("  ");
            }
            st1.Append("##");
            st2.Append("##");

            Console.SetCursorPosition(start_x, start_y + 1);
            Console.Write(st2);
            Console.SetCursorPosition(start_x, start_y + 2);
            Console.Write(st1);
            Console.SetCursorPosition(start_x, start_y + 3);
            Console.Write(st2);
            Console.SetCursorPosition(start_x, start_y + 4);
            Console.Write(st);
            Console.SetCursorPosition(0, 0);
        }
    }
}