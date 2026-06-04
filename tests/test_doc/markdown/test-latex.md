# LaTeX 完整兼容性测试

## 基础数学

这是行内公式测试：$E = mc^2$

### 分数和根号

行内分数：$\frac{a}{b}$

块级分数：
$$
\frac{-b \pm \sqrt{b^2-4ac}}{2a}
$$

平方根：$\sqrt{x}$

n次根：$\sqrt[n]{x}$

### 上下标

上标：$x^2$

下标：$x_n$

组合：$x_{ij}^{k}$

### 求和、积分、极限

求和：$\sum_{i=1}^{n} i = \frac{n(n+1)}{2}$

积分：$\int_{0}^{\infty} e^{-x^2} dx = \frac{\sqrt{\pi}}{2}$

极限：$\lim_{x \to \infty} \frac{1}{x} = 0$

### 希腊字母

$\alpha, \beta, \gamma, \delta, \epsilon, \theta, \lambda, \mu, \pi, \sigma, \omega$

大写：$\Alpha, \Beta, \Gamma, \Delta, \Theta, \Lambda, \Pi, \Sigma, \Omega$

### 运算符

加减乘除：$+, -, \times, \div$

比较：$<, >, \leq, \geq, \neq, \approx$

逻辑：$\land, \lor, \neg, \forall, \exists$

集合：$\in, \notin, \subset, \supset, \cup, \cap, \emptyset$

### 矩阵

$$
\begin{matrix}
a & b \\
c & d
\end{matrix}
$$

### 方程组

$$
\begin{cases}
x + y = 2 \\
2x - y = 1
\end{cases}
$$

### 特殊函数

$\sin(x), \cos(x), \tan(x), \log(x), \ln(x), e^x$

### 箭头

$\rightarrow, \leftarrow, \Rightarrow, \Leftarrow, \leftrightarrow$

### 特殊符号

$\infty, \partial, \nabla, \hbar, \ell$

### 带括号的复杂公式

$\left(\frac{a}{b}\right)^2$

$\left[\frac{x+y}{x-y}\right]$

### 多行公式

$$
f(x) = \int_{0}^{x} e^{-t^2} dt
= \frac{\sqrt{\pi}}{2} \operatorname{erf}(x)
$$

### 化学公式测试（如果支持）

$\ce{H2O}$

$\ce{CO2 + H2O -> C6H12O6 + O2}$

## 测试转义

价格：\$100

普通文本中的\$符号不应该渲染为公式
