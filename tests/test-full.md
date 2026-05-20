# WeaveDoc 全面测试文档

## 1. 文本格式

**粗体文本**、*斜体文本*、***粗斜体***、~~删除线~~、`行内代码`

## 2. 标题层级

### 三级标题

#### 四级标题

##### 五级标题

## 3. 列表

### 无序列表

- 第一项
- 第二项
  - 嵌套项 A
  - 嵌套项 B
- 第三项

### 有序列表

1. 步骤一
2. 步骤二
   1. 子步骤 2.1
   2. 子步骤 2.2
3. 步骤三

### 任务列表

- [x] 已完成任务
- [x] 已完成任务
- [ ] 未完成任务

## 4. 链接与引用

[Markdown 官方指南](https://www.markdownguide.org)

> 这是一段引用文本。
>
> 引用可以包含**格式**和`代码`。
>
> > 嵌套引用

## 5. 表格

| 姓名   | 年龄 | 城市   | 职业       |
|--------|------|--------|------------|
| 张三   | 28   | 北京   | 软件工程师 |
| 李四   | 32   | 上海   | 产品经理   |
| 王五   | 25   | 深圳   | UI 设计师  |
| 赵六   | 30   | 杭州   | 数据分析师 |

## 6. 代码块

```python
def fibonacci(n: int) -> list[int]:
    """生成斐波那契数列"""
    fib = [0, 1]
    for i in range(2, n):
        fib.append(fib[i-1] + fib[i-2])
    return fib[:n]

print(fibonacci(10))
```

```csharp
public class HelloWorld
{
    public static void Main(string[] args)
    {
        Console.WriteLine("Hello, WeaveDoc!");
        var numbers = Enumerable.Range(1, 100)
            .Where(n => n % 2 == 0)
            .Sum();
        Console.WriteLine($"偶数之和: {numbers}");
    }
}
```

```json
{
  "name": "WeaveDoc",
  "version": "1.0.0",
  "description": "语义化文档转换工具",
  "dependencies": {
    "Avalonia": "11.x",
    "OpenXML": "3.x"
  }
}
```

## 7. 图片

![示例图片](https://picsum.photos/600/300)

## 8. 分隔线

---

## 9. 数学公式（行内与块级）

行内公式：$E = mc^2$，勾股定理 $a^2 + b^2 = c^2$

论文常见行内公式：样本均值 $\bar{x}=\frac{1}{n}\sum_{i=1}^{n}x_i$，标准差 $s=\sqrt{\frac{1}{n-1}\sum_{i=1}^{n}(x_i-\bar{x})^2}$，向量范数 $\lVert \mathbf{x} \rVert_2$，角度 $\theta=30^\circ$，误差范围 $\pm 3\%$，化学式 $\mathrm{H_2O}$、$\mathrm{CO_2}$，上标引用 $^{[1]}$，圈号序号 $\textcircled{1}$、$\textcircled{2}$。

块级公式：

$$
\int_{-\infty}^{\infty} e^{-x^2} dx = \sqrt{\pi}
$$

$$
\sum_{i=1}^{n} i = \frac{n(n+1)}{2}
$$

### 9.1 上下标、分式、根式与括号

$$
x_{i,j}^{(k)}=\frac{\alpha_i+\beta_j}{\sqrt{1+\gamma_k^2}}
$$

$$
\left( \frac{a+b}{c+d} \right)^2
= \frac{(a+b)^2}{(c+d)^2}
$$

$$
\sqrt[n]{x^m} = x^{m/n}, \qquad
\frac{\partial^2 f}{\partial x^2}+\frac{\partial^2 f}{\partial y^2}=0
$$

### 9.2 希腊字母、关系符号与集合符号

$$
\alpha,\ \beta,\ \gamma,\ \delta,\ \epsilon,\ \varepsilon,\ \theta,\ \lambda,\ \mu,\ \pi,\ \rho,\ \sigma,\ \phi,\ \varphi,\ \omega
$$

$$
A \subseteq B,\quad x \in \mathbb{R},\quad
\forall x \in X,\ \exists y \in Y,\quad
A \cap B \neq \varnothing,\quad A \cup B = \Omega
$$

$$
f(x) \le g(x),\quad a \approx b,\quad
x \propto y,\quad m \equiv n \pmod{k}
$$

### 9.3 极限、导数、积分、求和与乘积

$$
\lim_{x \to 0}\frac{\sin x}{x}=1
$$

$$
\frac{d}{dx}\left(e^{ax}\right)=ae^{ax},\qquad
\frac{\partial z}{\partial x}=2x+y
$$

$$
\int_a^b f(x)\,dx,\qquad
\iint_D (x^2+y^2)\,dA,\qquad
\iiint_\Omega \rho(x,y,z)\,dV
$$

$$
\sum_{i=1}^{n} i^2=\frac{n(n+1)(2n+1)}{6},\qquad
\prod_{i=1}^{n} i=n!
$$

### 9.4 多行推导与对齐公式

$$
\begin{aligned}
S_n &= 1+2+\cdots+n \\
    &= \frac{n(n+1)}{2}
\end{aligned}
$$

$$
\begin{aligned}
\nabla \cdot \mathbf{E} &= \frac{\rho}{\varepsilon_0}, &
\nabla \cdot \mathbf{B} &= 0,\\
\nabla \times \mathbf{E} &= -\frac{\partial \mathbf{B}}{\partial t}, &
\nabla \times \mathbf{B} &= \mu_0\mathbf{J}+\mu_0\varepsilon_0\frac{\partial \mathbf{E}}{\partial t}
\end{aligned}
$$

### 9.5 分段函数、方程组与条件表达式

$$
f(x)=
\begin{cases}
x^2, & x \ge 0,\\
-x, & x < 0.
\end{cases}
$$

$$
\begin{cases}
2x+y=5,\\
x-y=1
\end{cases}
\Rightarrow
\begin{cases}
x=2,\\
y=1
\end{cases}
$$

### 9.6 矩阵、行列式与线性代数

$$
\mathbf{A}=
\begin{bmatrix}
1 & 2 & 3\\
4 & 5 & 6\\
7 & 8 & 9
\end{bmatrix},\qquad
\mathbf{x}=
\begin{bmatrix}
x_1\\
x_2\\
x_3
\end{bmatrix}
$$

$$
\det(\mathbf{A})=
\begin{vmatrix}
a_{11} & a_{12}\\
a_{21} & a_{22}
\end{vmatrix}
=a_{11}a_{22}-a_{12}a_{21}
$$

$$
\mathbf{A}^{-1}=\frac{1}{\det(\mathbf{A})}\operatorname{adj}(\mathbf{A}),\qquad
\mathbf{A}\mathbf{x}=\mathbf{b}
$$

$$
\lambda_i\mathbf{v}_i=\mathbf{A}\mathbf{v}_i,\qquad
\mathbf{Q}^{\mathsf{T}}\mathbf{Q}=\mathbf{I}
$$

### 9.7 概率统计与机器学习公式

$$
P(A\mid B)=\frac{P(B\mid A)P(A)}{P(B)}
$$

$$
\mathbb{E}[X]=\sum_{i=1}^{n}x_i p_i,\qquad
\operatorname{Var}(X)=\mathbb{E}\left[(X-\mathbb{E}[X])^2\right]
$$

$$
X \sim \mathcal{N}(\mu,\sigma^2),\qquad
p(x)=\frac{1}{\sqrt{2\pi\sigma^2}}\exp\left(-\frac{(x-\mu)^2}{2\sigma^2}\right)
$$

$$
\hat{\beta}=(\mathbf{X}^{\mathsf{T}}\mathbf{X})^{-1}\mathbf{X}^{\mathsf{T}}\mathbf{y}
$$

$$
J(\theta)=\frac{1}{2m}\sum_{i=1}^{m}\left(h_\theta(x^{(i)})-y^{(i)}\right)^2
$$

$$
\theta_{t+1}=\theta_t-\eta\nabla_\theta J(\theta_t)
$$

$$
\operatorname{softmax}(z_i)=\frac{e^{z_i}}{\sum_{j=1}^{K}e^{z_j}},\qquad
\mathcal{L}=-\sum_{i=1}^{K}y_i\log \hat{y}_i
$$

### 9.8 优化、约束与最值

$$
x^\star=\arg\min_{x\in\mathbb{R}^n} f(x)
$$

$$
\begin{aligned}
\min_{\mathbf{x}}\quad & \frac{1}{2}\mathbf{x}^{\mathsf{T}}\mathbf{Q}\mathbf{x}+\mathbf{c}^{\mathsf{T}}\mathbf{x}\\
\text{s.t.}\quad & \mathbf{A}\mathbf{x}\le \mathbf{b},\\
& \mathbf{x}\ge 0
\end{aligned}
$$

$$
\mathcal{L}(x,\lambda)=f(x)+\lambda g(x),\qquad
\nabla_x\mathcal{L}(x,\lambda)=0
$$

### 9.9 微分方程、傅里叶变换与拉普拉斯变换

$$
\frac{dy}{dt}=ky,\qquad y(t)=Ce^{kt}
$$

$$
\frac{\partial u}{\partial t}=\alpha\frac{\partial^2 u}{\partial x^2}
$$

$$
\mathcal{F}\{f(t)\}=\int_{-\infty}^{\infty} f(t)e^{-i\omega t}\,dt
$$

$$
\mathcal{L}\{f(t)\}=\int_0^\infty f(t)e^{-st}\,dt
$$

### 9.10 论文编号、引用、单位与常用文本函数

式（1）展示了归一化过程：

$$
z_i=\frac{x_i-\mu}{\sigma}
$$

式（2）展示了带单位的物理量：

$$
v=3.0\times 10^8\ \mathrm{m/s},\qquad
F=9.8\ \mathrm{N},\qquad
T=25^\circ\mathrm{C}
$$

式（3）展示了文本函数、罗马体与下标：

$$
\mathrm{ReLU}(x)=\max(0,x),\qquad
\mathrm{sp^2},\qquad
\mathrm{TiO_2},\qquad
\mathrm{Ni}\ 2\mathrm{p}_{3/2}
$$

式（4）展示了箭头、映射和推导关系：

$$
x \mapsto f(x),\qquad
A \Rightarrow B,\qquad
A \Longleftrightarrow B,\qquad
n\to\infty
$$

### 9.11 LaTeX 重音、装饰与上下括注

$$
\hat{x},\quad \widehat{ABC},\quad
\tilde{x},\quad \widetilde{XYZ},\quad
\bar{x},\quad \overline{AB},\quad
\vec{v},\quad \dot{x},\quad \ddot{x}
$$

$$
\underbrace{x_1+x_2+\cdots+x_n}_{n\ \text{terms}},\qquad
\overbrace{a+b+\cdots+z}^{26\ \text{letters}}
$$

$$
\overset{\Delta}{=},\qquad
\underset{x\to 0}{\lim},\qquad
\xrightarrow{k\to\infty},\qquad
\xleftarrow{\text{reverse}}
$$

### 9.12 二项式、组合数、特殊矩阵与小矩阵

$$
\binom{n}{k}=\frac{n!}{k!(n-k)!},\qquad
{n \choose k}={n \choose n-k}
$$

$$
\begin{pmatrix}
a & b\\
c & d
\end{pmatrix},\qquad
\begin{Bmatrix}
1 & 0\\
0 & 1
\end{Bmatrix},\qquad
\begin{smallmatrix}
1 & 2\\
3 & 4
\end{smallmatrix}
$$

$$
\left[
\begin{array}{cc|c}
1 & 2 & 3\\
4 & 5 & 6
\end{array}
\right]
$$

### 9.13 自定义算子、文本、空格与常见函数

$$
\operatorname{rank}(\mathbf{A}),\qquad
\operatorname*{arg\,max}_{x\in X} f(x),\qquad
\mathrm{d}x,\qquad
\text{if } x>0
$$

$$
\log_2 n,\quad \ln x,\quad \sin x,\quad \cos x,\quad
\tan x,\quad \arctan x,\quad \exp(x)
$$

$$
a\,b,\quad a\;b,\quad a\quad b,\quad a\qquad b,\quad
\left\langle \mathbf{x},\mathbf{y}\right\rangle
$$

### 9.14 公式编号、标签、盒子与长公式

$$
\boxed{E=mc^2}
$$

$$
\begin{aligned}
\mathcal{J}(\theta)
&= -\frac{1}{m}\sum_{i=1}^{m}
\left[
y^{(i)}\log h_\theta(x^{(i)})
+(1-y^{(i)})\log\left(1-h_\theta(x^{(i)})\right)
\right] \\
&\quad + \frac{\lambda}{2m}\sum_{j=1}^{n}\theta_j^2
\end{aligned}
$$

$$
\begin{equation}
\nabla^2 \phi = \frac{\partial^2 \phi}{\partial x^2}
+ \frac{\partial^2 \phi}{\partial y^2}
+ \frac{\partial^2 \phi}{\partial z^2}
\tag{9-1}
\end{equation}
$$

### 9.15 物理、控制与工程论文常见符号

$$
G(s)=\frac{Y(s)}{U(s)}=\frac{K}{Ts+1},\qquad
H(z)=\sum_{n=-\infty}^{\infty}h[n]z^{-n}
$$

$$
\mathbf{u}(t)=-\mathbf{K}\mathbf{x}(t),\qquad
\dot{\mathbf{x}}(t)=\mathbf{A}\mathbf{x}(t)+\mathbf{B}\mathbf{u}(t)
$$

$$
\mathrm{SNR}=10\log_{10}\left(\frac{P_s}{P_n}\right)\ \mathrm{dB},\qquad
\eta=\frac{P_{\mathrm{out}}}{P_{\mathrm{in}}}\times 100\%
$$

$$
\sigma=\frac{F}{A},\qquad
\varepsilon=\frac{\Delta L}{L_0},\qquad
E=\frac{\sigma}{\varepsilon}
$$

## 10. 脚注

这是一段带脚注的文本[^1]，还有第二个脚注[^2]。

[^1]: 这是第一个脚注的内容。
[^2]: 这是第二个脚注的内容，支持**格式**。

## 11. 混合复杂内容

以下是一个综合段落，包含**粗体**、*斜体*、`代码`、[链接](https://example.com)和~~删除线~~的混合使用。

| 功能     | 状态 | 备注              |
|----------|------|-------------------|
| DOCX 导出 | ✅   | 已完成             |
| PDF 导出  | ✅   | 已完成             |
| 模板管理  | ✅   | 支持自定义导入     |

> **注意**：以上表格中的状态符号在纯 Markdown 中为文本，实际渲染取决于样式模板。

## 12. 转义字符

\*这不是斜体\*  \|这不是表格\|  \#这不是标题

## 13. HTML 内嵌

<div style="border:1px solid #ccc; padding:10px; margin:10px 0;">
  <p>这是一段 <strong>HTML 内嵌</strong> 内容，用于测试混合渲染。</p>
</div>

## 14. 长段落与中文排版

Markdown 是一种轻量级标记语言，由 John Gruber 于 2004 年创建。它允许人们使用易读易写的纯文本格式编写文档，然后转换成有效的 XHTML（或者 HTML）文档。Markdown 的语法简洁明了，学习成本低，因此被广泛应用于技术文档撰写、博客发布、README 文件等场景。

在中文排版中，我们通常需要注意首行缩进、行间距和字间距等细节。中英文混排时，建议在中文字符与英文字母之间添加一个空格，例如使用 WeaveDoc 进行文档转换，以提升阅读体验。

## 15. 嵌套列表

1. 第一层级
   - 无序子项 A
   - 无序子项 B
     - 更深层级 i
     - 更深层级 ii
2. 第二层级
   1. 有序子项 1
   2. 有序子项 2
3. 第三层级
