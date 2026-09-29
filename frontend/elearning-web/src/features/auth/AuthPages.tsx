import { useState } from "react";
import { Alert, Button, Card, Form, Spinner } from "react-bootstrap";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { Link, Navigate, useLocation, useNavigate } from "react-router";
import { useTranslation } from "react-i18next";
import { authApi } from "../../services/api";
import { toApiError } from "../../services/apiClient";
import { describeError } from "../../utils/errors";
import { resolveLoginRedirect, useAuth, type LoginRedirectState } from "./useAuth";

const loginSchema = z.object({
  userName: z.string().trim().min(1, "Vui lòng nhập tên đăng nhập."),
  password: z.string().min(1, "Vui lòng nhập mật khẩu."),
});

export function LoginPage() {
  const { t } = useTranslation();
  const { login, status, user: currentUser } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [error, setError] = useState<string | null>(null);
  const redirectState = location.state as LoginRedirectState | null;
  const notice = redirectState?.notice;
  const { register, handleSubmit, formState } = useForm<z.infer<typeof loginSchema>>({ resolver: zodResolver(loginSchema) });

  if (status === "authenticated" && currentUser) {
    return <Navigate to={resolveLoginRedirect(redirectState, currentUser.id)} replace />;
  }

  const onSubmit = handleSubmit(async (values) => {
    setError(null);
    try {
      const user = await login(values.userName, values.password);
      navigate(user.mustChangePassword ? "/change-password" : resolveLoginRedirect(redirectState, user.id), { replace: true });
    } catch (e) {
      setError(describeError(e));
    }
  });

  return (
    <Card>
      <Card.Body>
        <h2 className="h5 mb-3">{t("auth.login")}</h2>
        {notice && <Alert variant="success">{notice}</Alert>}
        {error && <Alert variant="danger">{error}</Alert>}
        <Form noValidate onSubmit={onSubmit}>
          <Form.Group className="mb-3" controlId="login-username">
            <Form.Label>{t("auth.userName")}</Form.Label>
            <Form.Control autoComplete="username" autoFocus isInvalid={!!formState.errors.userName} {...register("userName")} />
            <Form.Control.Feedback type="invalid">{formState.errors.userName?.message}</Form.Control.Feedback>
          </Form.Group>
          <Form.Group className="mb-3" controlId="login-password">
            <Form.Label>{t("auth.password")}</Form.Label>
            <Form.Control type="password" autoComplete="current-password" isInvalid={!!formState.errors.password} {...register("password")} />
            <Form.Control.Feedback type="invalid">{formState.errors.password?.message}</Form.Control.Feedback>
          </Form.Group>
          <Button type="submit" className="w-100" disabled={formState.isSubmitting}>
            {formState.isSubmitting && <Spinner size="sm" animation="border" className="me-2" />}
            {t("auth.login")}
          </Button>
        </Form>
        <div className="d-flex justify-content-between mt-3 small">
          <Link to="/forgot-password">{t("auth.forgotPassword")}</Link>
          <span>
            {t("auth.noAccount")} <Link to="/register">{t("auth.register")}</Link>
          </span>
        </div>
      </Card.Body>
    </Card>
  );
}

const registerSchema = z
  .object({
    userName: z.string().regex(/^[A-Za-z0-9._-]{3,50}$/, "Tên đăng nhập gồm 3–50 ký tự: chữ không dấu, số, dấu chấm, gạch dưới, gạch ngang."),
    email: z.email("Email không hợp lệ."),
    fullName: z.string().trim().min(1, "Vui lòng nhập họ tên.").max(200),
    password: z.string().min(8, "Mật khẩu phải có ít nhất 8 ký tự.").max(128),
    confirmPassword: z.string(),
    acceptTerms: z.boolean().refine((v) => v, "Bạn cần đồng ý điều khoản."),
  })
  .refine((v) => v.password === v.confirmPassword, { path: ["confirmPassword"], message: "Mật khẩu nhập lại không khớp." })
  .refine((v) => v.password.toLowerCase() !== v.userName.toLowerCase(), { path: ["password"], message: "Mật khẩu không được trùng tên đăng nhập." });

export function RegisterPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const [error, setError] = useState<string | null>(null);
  const { register, handleSubmit, formState, setError: setFieldError } = useForm<z.infer<typeof registerSchema>>({
    resolver: zodResolver(registerSchema),
    defaultValues: { acceptTerms: false },
  });

  const onSubmit = handleSubmit(async (values) => {
    setError(null);
    try {
      await authApi.register({
        userName: values.userName,
        email: values.email,
        fullName: values.fullName,
        password: values.password,
        acceptTerms: values.acceptTerms,
      });
      navigate("/login", { replace: true, state: { notice: t("auth.registered") } });
    } catch (e) {
      const apiError = toApiError(e);
      Object.entries(apiError.fieldErrors()).forEach(([field, message]) =>
        setFieldError(field as keyof z.infer<typeof registerSchema>, { message }),
      );
      setError(describeError(apiError));
    }
  });

  const field = (name: "userName" | "email" | "fullName" | "password" | "confirmPassword", label: string, type = "text", autoComplete?: string) => (
    <Form.Group className="mb-3" controlId={`register-${name}`}>
      <Form.Label>{label}</Form.Label>
      <Form.Control type={type} autoComplete={autoComplete} isInvalid={!!formState.errors[name]} {...register(name)} />
      <Form.Control.Feedback type="invalid">{formState.errors[name]?.message}</Form.Control.Feedback>
    </Form.Group>
  );

  return (
    <Card>
      <Card.Body>
        <h2 className="h5 mb-3">{t("auth.register")}</h2>
        {error && <Alert variant="danger">{error}</Alert>}
        <Form noValidate onSubmit={onSubmit}>
          {field("userName", t("auth.userNameOnly"), "text", "username")}
          {field("email", t("auth.email"), "email", "email")}
          {field("fullName", t("auth.fullName"), "text", "name")}
          {field("password", t("auth.password"), "password", "new-password")}
          {field("confirmPassword", t("auth.confirmPassword"), "password", "new-password")}
          <Form.Group className="mb-3" controlId="register-terms">
            <Form.Check isInvalid={!!formState.errors.acceptTerms} label={t("auth.acceptTerms")} {...register("acceptTerms")} />
            {formState.errors.acceptTerms && <div className="invalid-feedback d-block">{formState.errors.acceptTerms.message}</div>}
          </Form.Group>
          <Button type="submit" className="w-100" disabled={formState.isSubmitting}>
            {t("auth.register")}
          </Button>
        </Form>
        <div className="mt-3 small text-center">
          {t("auth.haveAccount")} <Link to="/login">{t("auth.login")}</Link>
        </div>
      </Card.Body>
    </Card>
  );
}

const changePasswordSchema = z
  .object({
    currentPassword: z.string().min(1, "Vui lòng nhập mật khẩu hiện tại."),
    newPassword: z.string().min(8, "Mật khẩu phải có ít nhất 8 ký tự.").max(128),
    confirmPassword: z.string(),
  })
  .refine((v) => v.newPassword === v.confirmPassword, { path: ["confirmPassword"], message: "Mật khẩu nhập lại không khớp." })
  .refine((v) => v.newPassword !== v.currentPassword, { path: ["newPassword"], message: "Mật khẩu mới phải khác mật khẩu hiện tại." });

export function ChangePasswordPage() {
  const { t } = useTranslation();
  const { user, applySession } = useAuth();
  const navigate = useNavigate();
  const [error, setError] = useState<string | null>(null);
  const { register, handleSubmit, formState } = useForm<z.infer<typeof changePasswordSchema>>({ resolver: zodResolver(changePasswordSchema) });

  const onSubmit = handleSubmit(async (values) => {
    setError(null);
    try {
      const session = await authApi.changePassword(values.currentPassword, values.newPassword);
      applySession(session);
      navigate("/", { replace: true, state: { notice: t("auth.passwordChanged") } });
    } catch (e) {
      setError(describeError(e));
    }
  });

  return (
    <Card className="mx-auto" style={{ maxWidth: 440 }}>
      <Card.Body>
        <h2 className="h5 mb-3">{t("auth.changePassword")}</h2>
        {user?.mustChangePassword && <Alert variant="warning">{t("auth.mustChangePassword")}</Alert>}
        {error && <Alert variant="danger">{error}</Alert>}
        <Form noValidate onSubmit={onSubmit}>
          {(
            [
              ["currentPassword", t("auth.currentPassword"), "current-password"],
              ["newPassword", t("auth.newPassword"), "new-password"],
              ["confirmPassword", t("auth.confirmPassword"), "new-password"],
            ] as const
          ).map(([name, label, autoComplete]) => (
            <Form.Group className="mb-3" controlId={`cp-${name}`} key={name}>
              <Form.Label>{label}</Form.Label>
              <Form.Control type="password" autoComplete={autoComplete} isInvalid={!!formState.errors[name]} {...register(name)} />
              <Form.Control.Feedback type="invalid">{formState.errors[name]?.message}</Form.Control.Feedback>
            </Form.Group>
          ))}
          <Button type="submit" className="w-100" disabled={formState.isSubmitting}>
            {t("auth.changePassword")}
          </Button>
        </Form>
      </Card.Body>
    </Card>
  );
}

export function ForgotPasswordPage() {
  const { t } = useTranslation();
  const [email, setEmail] = useState("");
  const [message, setMessage] = useState<string | null>(null);
  return (
    <Card>
      <Card.Body>
        <h2 className="h5 mb-3">{t("auth.forgotPassword")}</h2>
        {message ? (
          <Alert variant="info">{message}</Alert>
        ) : (
          <Form
            onSubmit={async (e) => {
              e.preventDefault();
              setMessage(await authApi.forgotPassword(email).catch(() => "Vui lòng liên hệ quản trị viên để được đặt lại mật khẩu."));
            }}
          >
            <Form.Group className="mb-3" controlId="forgot-email">
              <Form.Label>{t("auth.email")}</Form.Label>
              <Form.Control type="email" value={email} onChange={(e) => setEmail(e.target.value)} required />
            </Form.Group>
            <Button type="submit" className="w-100">
              {t("common.confirm")}
            </Button>
          </Form>
        )}
        <div className="mt-3 small text-center">
          <Link to="/login">{t("auth.login")}</Link>
        </div>
      </Card.Body>
    </Card>
  );
}

export function ProfilePage() {
  const { t } = useTranslation();
  const { user } = useAuth();
  if (!user) {
    return null;
  }
  return (
    <Card style={{ maxWidth: 560 }}>
      <Card.Body>
        <h1 className="h5">{t("nav.profile")}</h1>
        <dl className="row mb-3">
          <dt className="col-sm-4">{t("auth.userNameOnly")}</dt>
          <dd className="col-sm-8">{user.userName}</dd>
          <dt className="col-sm-4">{t("auth.fullName")}</dt>
          <dd className="col-sm-8">{user.fullName}</dd>
          <dt className="col-sm-4">{t("auth.email")}</dt>
          <dd className="col-sm-8">{user.email}</dd>
          <dt className="col-sm-4">{t("nav.roles")}</dt>
          <dd className="col-sm-8">{user.roles.join(", ")}</dd>
        </dl>
        <Link to="/change-password" className="btn btn-outline-primary btn-sm">
          {t("auth.changePassword")}
        </Link>
      </Card.Body>
    </Card>
  );
}
