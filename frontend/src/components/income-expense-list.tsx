"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  ArrowDownLeft,
  ArrowUpRight,
  Loader2,
  Plus,
  Settings2,
  Trash2,
  TrendingDown,
  TrendingUp,
} from "lucide-react";
import { useState } from "react";
import { toast } from "sonner";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Switch } from "@/components/ui/switch";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { api } from "@/lib/api";
import { SIDES, SIDE_LABELS, monthLabel } from "@/lib/income-expense";
import { formatDate, formatMoney, isoToDateInput, parseMoneyInput } from "@/lib/parties";
import type {
  AccountDto,
  IncomeExpenseCategoryDto,
  IncomeExpenseRecordDto,
  IncomeExpenseSideDto,
  IncomeExpenseSummaryResponse,
  PagedResponse,
} from "@/lib/types";

const PAGE_SIZE = 20;

/** Kayıtları, kategorileri, ekstreleri ve hesap bakiyelerini birlikte geçersiz kıl. */
function invalidateIncomeExpense(queryClient: ReturnType<typeof useQueryClient>) {
  queryClient.invalidateQueries({ queryKey: ["income-expense-records"] });
  queryClient.invalidateQueries({ queryKey: ["income-expense-categories"] });
  queryClient.invalidateQueries({ queryKey: ["income-expense-summary"] });
  queryClient.invalidateQueries({ queryKey: ["accounts"] });
  queryClient.invalidateQueries({ queryKey: ["account"] });
  queryClient.invalidateQueries({ queryKey: ["account-statement"] });
}

/** Dönem başlangıcı: 5 ay önceki ayın ilk günü (varsayılan son 6 ay). */
function defaultFrom(): string {
  const now = new Date();
  return isoToDateInput(new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth() - 5, 1)).toISOString());
}

/** Gelir & Gider: dönem özeti, filtreli kayıt listesi, kayıt ve kategori yönetimi. */
export function IncomeExpenseList() {
  const queryClient = useQueryClient();

  // Dönem ve liste filtreleri.
  const [from, setFrom] = useState(defaultFrom);
  const [to, setTo] = useState(() => isoToDateInput(new Date().toISOString()));
  const [type, setType] = useState("all");
  const [categoryId, setCategoryId] = useState("all");
  const [page, setPage] = useState(1);

  const [recordSide, setRecordSide] = useState<IncomeExpenseSideDto | null>(null);
  const [cancelTarget, setCancelTarget] = useState<IncomeExpenseRecordDto | null>(null);
  const [categoriesOpen, setCategoriesOpen] = useState(false);

  const params = new URLSearchParams({
    page: String(page),
    pageSize: String(PAGE_SIZE),
    from: `${from}T00:00:00Z`,
    to: `${to}T00:00:00Z`,
  });
  if (type !== "all") {
    params.set("type", type);
  }
  if (categoryId !== "all") {
    params.set("categoryId", categoryId);
  }

  const summary = useQuery({
    queryKey: ["income-expense-summary", from, to],
    queryFn: () =>
      api<IncomeExpenseSummaryResponse>(
        `/api/v1/income-expense/summary?from=${from}T00:00:00Z&to=${to}T00:00:00Z`,
      ),
  });

  const records = useQuery({
    queryKey: ["income-expense-records", type, categoryId, from, to, page],
    queryFn: () => api<PagedResponse<IncomeExpenseRecordDto>>(`/api/v1/income-expense/records?${params.toString()}`),
  });

  const categories = useQuery({
    queryKey: ["income-expense-categories"],
    queryFn: () => api<IncomeExpenseCategoryDto[]>("/api/v1/income-expense/categories"),
  });

  const accounts = useQuery({
    queryKey: ["accounts"],
    queryFn: () => api<AccountDto[]>("/api/v1/accounts"),
  });

  const cancelRecord = useMutation({
    mutationFn: (id: string) => api(`/api/v1/income-expense/records/${id}/cancel`, { method: "POST" }),
    onSuccess: () => {
      invalidateIncomeExpense(queryClient);
      toast.success("Kayıt iptal edildi — kasa hareketinin tersi yazıldı.");
      setCancelTarget(null);
    },
    onError: (mutationError) =>
      toast.error(mutationError instanceof Error ? mutationError.message : "Kayıt iptal edilemedi."),
  });

  // Kategori seçenekleri tür filtresiyle sınırlanır; tür değişince seçim sıfırlanır.
  const categoryOptions = (categories.data ?? []).filter(
    (category) => type === "all" || category.type === type,
  );

  const totalPages = records.data ? Math.max(1, Math.ceil(records.data.totalCount / records.data.pageSize)) : 1;
  const data = records.data;
  const activeAccounts = (accounts.data ?? []).filter((account) => account.isActive);

  return (
    <div className="mx-auto grid w-full max-w-6xl gap-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Gelir &amp; Gider</h1>
          <p className="text-sm text-muted-foreground">
            {summary.data
              ? `${monthLabel(summary.data.months[0].year, summary.data.months[0].month)} – ${monthLabel(
                  summary.data.months[summary.data.months.length - 1].year,
                  summary.data.months[summary.data.months.length - 1].month,
                )} · net ${formatMoney(summary.data.net)}`
              : "Yükleniyor..."}
          </p>
        </div>
        <div className="flex flex-wrap gap-2">
          <Button variant="outline" onClick={() => setCategoriesOpen(true)}>
            <Settings2 className="size-4" />
            Kategoriler
          </Button>
          <Button
            variant="outline"
            onClick={() => setRecordSide("Expense")}
            className="text-destructive hover:text-destructive"
          >
            <ArrowUpRight className="size-4" />
            Gider Ekle
          </Button>
          <Button onClick={() => setRecordSide("Income")}>
            <ArrowDownLeft className="size-4" />
            Gelir Ekle
          </Button>
        </div>
      </div>

      {/* Dönem özeti kartları. */}
      <div className="grid gap-4 sm:grid-cols-3">
        <Card>
          <CardContent className="grid gap-1">
            <span className="flex items-center gap-1 text-xs text-muted-foreground">
              <TrendingUp className="size-3.5" />
              Dönem Geliri
            </span>
            <span className="text-xl font-semibold tabular-nums text-emerald-600 dark:text-emerald-400">
              {summary.data ? formatMoney(summary.data.totalIncome) : "—"}
            </span>
          </CardContent>
        </Card>
        <Card>
          <CardContent className="grid gap-1">
            <span className="flex items-center gap-1 text-xs text-muted-foreground">
              <TrendingDown className="size-3.5" />
              Dönem Gideri
            </span>
            <span className="text-xl font-semibold tabular-nums text-destructive">
              {summary.data ? formatMoney(summary.data.totalExpense) : "—"}
            </span>
          </CardContent>
        </Card>
        <Card>
          <CardContent className="grid gap-1">
            <span className="text-xs text-muted-foreground">Net</span>
            <span
              className={`text-xl font-semibold tabular-nums ${
                (summary.data?.net ?? 0) < 0 ? "text-destructive" : ""
              }`}
            >
              {summary.data ? formatMoney(summary.data.net) : "—"}
            </span>
          </CardContent>
        </Card>
      </div>

      {/* Dönem + tür + kategori filtreleri. */}
      <div className="flex flex-wrap items-center gap-3">
        <div className="grid gap-1">
          <span className="text-xs text-muted-foreground">Dönem</span>
          <div className="flex items-center gap-2">
            <Input
              aria-label="Başlangıç tarihi"
              type="date"
              className="w-40"
              value={from}
              onChange={(event) => {
                setFrom(event.target.value);
                setPage(1);
              }}
            />
            <span className="text-sm text-muted-foreground">–</span>
            <Input
              aria-label="Bitiş tarihi"
              type="date"
              className="w-40"
              value={to}
              onChange={(event) => {
                setTo(event.target.value);
                setPage(1);
              }}
            />
          </div>
        </div>
        <div className="grid gap-1">
          <span className="text-xs text-muted-foreground">Tür</span>
          <Select
            value={type}
            onValueChange={(value) => {
              setType(value);
              setCategoryId("all");
              setPage(1);
            }}
          >
            <SelectTrigger className="w-32" aria-label="Tür filtresi">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">Tümü</SelectItem>
              {SIDES.map((side) => (
                <SelectItem key={side.value} value={side.value}>
                  {side.label}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
        <div className="grid gap-1">
          <span className="text-xs text-muted-foreground">Kategori</span>
          <Select
            value={categoryId}
            onValueChange={(value) => {
              setCategoryId(value);
              setPage(1);
            }}
          >
            <SelectTrigger className="w-44" aria-label="Kategori filtresi">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">Tüm kategoriler</SelectItem>
              {categoryOptions.map((category) => (
                <SelectItem key={category.id} value={category.id}>
                  {SIDE_LABELS[category.type]} · {category.name}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
      </div>

      {/* Kayıt listesi. */}
      {records.isPending ? (
        <div className="flex justify-center py-12">
          <Loader2 className="size-6 animate-spin text-muted-foreground" />
        </div>
      ) : records.isError ? (
        <Card>
          <CardContent className="py-8 text-center text-sm text-destructive">
            {records.error instanceof Error ? records.error.message : "Kayıt listesi alınamadı."}
          </CardContent>
        </Card>
      ) : data && data.items.length > 0 ? (
        <div className="rounded-lg border">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Tarih</TableHead>
                <TableHead>Tür</TableHead>
                <TableHead className="hidden sm:table-cell">Kategori</TableHead>
                <TableHead className="hidden md:table-cell">Hesap</TableHead>
                <TableHead className="hidden max-w-56 truncate lg:table-cell">Açıklama</TableHead>
                <TableHead className="text-right">Tutar</TableHead>
                <TableHead className="hidden text-right xl:table-cell">Durum</TableHead>
                <TableHead className="text-right" />
              </TableRow>
            </TableHeader>
            <TableBody>
              {data.items.map((record) => (
                <TableRow key={record.id} className={record.status === "Cancelled" ? "opacity-60" : ""}>
                  <TableCell className="text-muted-foreground">{formatDate(record.date)}</TableCell>
                  <TableCell>
                    <Badge variant={record.type === "Income" ? "default" : "secondary"}>
                      {SIDE_LABELS[record.type]}
                    </Badge>
                  </TableCell>
                  <TableCell className="hidden sm:table-cell">{record.categoryName}</TableCell>
                  <TableCell className="hidden text-muted-foreground md:table-cell">
                    {record.paymentAccountName}
                  </TableCell>
                  <TableCell className="hidden max-w-56 truncate text-muted-foreground lg:table-cell">
                    {record.description ?? "—"}
                  </TableCell>
                  <TableCell
                    className={`text-right tabular-nums ${
                      record.type === "Income" ? "text-emerald-600 dark:text-emerald-400" : "text-destructive"
                    }`}
                  >
                    {record.type === "Income" ? "+" : "−"}
                    {formatMoney(record.amount)}
                  </TableCell>
                  <TableCell className="hidden text-right xl:table-cell">
                    {record.status === "Cancelled" ? (
                      <Badge variant="outline">İptal</Badge>
                    ) : (
                      <span className="text-muted-foreground">Aktif</span>
                    )}
                  </TableCell>
                  <TableCell className="text-right">
                    {record.status === "Active" && (
                      <Button
                        variant="ghost"
                        size="sm"
                        className="text-destructive hover:text-destructive"
                        onClick={() => setCancelTarget(record)}
                      >
                        İptal
                      </Button>
                    )}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
      ) : (
        <Card>
          <CardContent className="grid justify-items-center gap-3 py-10 text-center">
            <p className="text-sm text-muted-foreground">
              Bu filtrelerle kayıt yok. Gelir ya da gider ekleyerek başlayın.
            </p>
            <div className="flex gap-2">
              <Button variant="outline" onClick={() => setRecordSide("Expense")}>
                <Plus className="size-4" />
                Gider Ekle
              </Button>
              <Button onClick={() => setRecordSide("Income")}>
                <Plus className="size-4" />
                Gelir Ekle
              </Button>
            </div>
          </CardContent>
        </Card>
      )}

      {data && data.totalCount > data.pageSize && (
        <div className="flex items-center justify-between text-sm">
          <span className="text-muted-foreground">
            Sayfa {data.page} / {totalPages} · {data.totalCount} kayıt
          </span>
          <div className="flex gap-2">
            <Button variant="outline" size="sm" disabled={page <= 1} onClick={() => setPage(page - 1)}>
              Önceki
            </Button>
            <Button variant="outline" size="sm" disabled={page >= totalPages} onClick={() => setPage(page + 1)}>
              Sonraki
            </Button>
          </div>
        </div>
      )}

      {/* Gelir/gider ekleme diyalogu — taraf düğmeye göre sabitlenir. */}
      {recordSide && (
        <RecordDialog
          side={recordSide}
          categories={(categories.data ?? []).filter((c) => c.type === recordSide && c.isActive)}
          accounts={activeAccounts}
          onClose={() => setRecordSide(null)}
        />
      )}

      {/* Kayıt iptal onayı. */}
      <Dialog open={cancelTarget !== null} onOpenChange={(open) => !open && setCancelTarget(null)}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>Kaydı İptal Et</DialogTitle>
            <DialogDescription>
              {cancelTarget && (
                <>
                  {SIDE_LABELS[cancelTarget.type]} · {cancelTarget.categoryName} ·{" "}
                  {formatMoney(cancelTarget.amount)} — kasa hareketinin tersi yazılır, kayıt değiştirilemez
                  biçimde iptal kalır.
                </>
              )}
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button variant="outline" onClick={() => setCancelTarget(null)} disabled={cancelRecord.isPending}>
              Vazgeç
            </Button>
            <Button
              variant="destructive"
              disabled={cancelRecord.isPending}
              onClick={() => cancelTarget && cancelRecord.mutate(cancelTarget.id)}
            >
              {cancelRecord.isPending && <Loader2 className="size-4 animate-spin" />}
              İptal Et
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Kategori yönetimi. */}
      {categoriesOpen && <CategoryDialog onClose={() => setCategoriesOpen(false)} />}
    </div>
  );
}

/** Gelir ya da gider kaydı formu — kasa hareketiyle birlikte yazar. */
function RecordDialog({
  side,
  categories,
  accounts,
  onClose,
}: {
  side: IncomeExpenseSideDto;
  categories: IncomeExpenseCategoryDto[];
  accounts: AccountDto[];
  onClose: () => void;
}) {
  const queryClient = useQueryClient();
  const [categoryId, setCategoryId] = useState("");
  const [accountId, setAccountId] = useState("default");
  const [amount, setAmount] = useState("");
  const [date, setDate] = useState(() => isoToDateInput(new Date().toISOString()));
  const [description, setDescription] = useState("");
  const [documentNumber, setDocumentNumber] = useState("");

  const amountInvalid = parseMoneyInput(amount) === null || (parseMoneyInput(amount) ?? 0) <= 0;
  const invalid = !categoryId || amountInvalid;

  const create = useMutation({
    mutationFn: () =>
      api<IncomeExpenseRecordDto>("/api/v1/income-expense/records", {
        method: "POST",
        body: JSON.stringify({
          type: side,
          categoryId,
          amount: parseMoneyInput(amount),
          date: `${date}T00:00:00Z`,
          paymentAccountId: accountId === "default" ? null : accountId,
          description: description || null,
          documentNumber: documentNumber || null,
        }),
      }),
    onSuccess: () => {
      invalidateIncomeExpense(queryClient);
      toast.success(`${SIDE_LABELS[side]} kaydı eklendi.`);
      onClose();
    },
    onError: (mutationError) =>
      toast.error(mutationError instanceof Error ? mutationError.message : "Kayıt eklenemedi."),
  });

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>{SIDE_LABELS[side]} Ekle</DialogTitle>
          <DialogDescription>
            Kayıt seçtiğiniz kasa/banka hesabına {side === "Income" ? "giriş" : "çıkış"} hareketi yazar;
            sonradan değiştirilemez — düzeltme iptalle yapılır.
          </DialogDescription>
        </DialogHeader>
        <div className="grid gap-4">
          <div className="grid gap-2">
            <Label htmlFor="record-category">Kategori *</Label>
            <Select value={categoryId || undefined} onValueChange={setCategoryId}>
              <SelectTrigger id="record-category">
                <SelectValue placeholder="Kategori seçin" />
              </SelectTrigger>
              <SelectContent>
                {categories.map((category) => (
                  <SelectItem key={category.id} value={category.id}>
                    {category.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="grid gap-2">
            <Label htmlFor="record-account">Hesap</Label>
            <Select value={accountId} onValueChange={setAccountId}>
              <SelectTrigger id="record-account">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="default">Varsayılan Kasa</SelectItem>
                {accounts.map((account) => (
                  <SelectItem key={account.id} value={account.id}>
                    {account.name} ({formatMoney(account.currentBalance)})
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="grid gap-2 sm:grid-cols-2">
            <div className="grid gap-2">
              <Label htmlFor="record-amount">Tutar *</Label>
              <Input
                id="record-amount"
                inputMode="decimal"
                placeholder="0,00"
                value={amount}
                onChange={(event) => setAmount(event.target.value)}
              />
            </div>
            <div className="grid gap-2">
              <Label htmlFor="record-date">Tarih *</Label>
              <Input
                id="record-date"
                type="date"
                value={date}
                onChange={(event) => setDate(event.target.value)}
              />
            </div>
          </div>
          <div className="grid gap-2">
            <Label htmlFor="record-description">Açıklama</Label>
            <Input
              id="record-description"
              placeholder="Dükkan kirası, elektrik faturası..."
              value={description}
              onChange={(event) => setDescription(event.target.value)}
            />
          </div>
          <div className="grid gap-2">
            <Label htmlFor="record-document">Belge No</Label>
            <Input
              id="record-document"
              placeholder="Fatura/makbuz numarası (opsiyonel)"
              value={documentNumber}
              onChange={(event) => setDocumentNumber(event.target.value)}
            />
          </div>
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={onClose} disabled={create.isPending}>
            Vazgeç
          </Button>
          <Button disabled={create.isPending || invalid} onClick={() => create.mutate()}>
            {create.isPending && <Loader2 className="size-4 animate-spin" />}
            Kaydet
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

/** Kategori yönetimi: ekleme, ad/aktiflik düzenleme, kayıtsızı silme. */
function CategoryDialog({ onClose }: { onClose: () => void }) {
  const queryClient = useQueryClient();
  const [name, setName] = useState("");
  const [side, setSide] = useState<IncomeExpenseSideDto>("Expense");
  const [editing, setEditing] = useState<IncomeExpenseCategoryDto | null>(null);
  const [editName, setEditName] = useState("");
  const [editActive, setEditActive] = useState(true);

  const { data: categories } = useQuery({
    queryKey: ["income-expense-categories"],
    queryFn: () => api<IncomeExpenseCategoryDto[]>("/api/v1/income-expense/categories"),
  });

  const invalidate = () => {
    queryClient.invalidateQueries({ queryKey: ["income-expense-categories"] });
    queryClient.invalidateQueries({ queryKey: ["income-expense-records"] });
  };

  const create = useMutation({
    mutationFn: () =>
      api<IncomeExpenseCategoryDto>("/api/v1/income-expense/categories", {
        method: "POST",
        body: JSON.stringify({ name: name.trim(), type: side }),
      }),
    onSuccess: () => {
      invalidate();
      toast.success("Kategori eklendi.");
      setName("");
    },
    onError: (mutationError) =>
      toast.error(mutationError instanceof Error ? mutationError.message : "Kategori eklenemedi."),
  });

  const update = useMutation({
    mutationFn: () =>
      api<IncomeExpenseCategoryDto>(`/api/v1/income-expense/categories/${editing!.id}`, {
        method: "PUT",
        body: JSON.stringify({ name: editName.trim(), isActive: editActive }),
      }),
    onSuccess: () => {
      invalidate();
      toast.success("Kategori güncellendi.");
      setEditing(null);
    },
    onError: (mutationError) =>
      toast.error(mutationError instanceof Error ? mutationError.message : "Kategori güncellenemedi."),
  });

  const remove = useMutation({
    mutationFn: (id: string) => api(`/api/v1/income-expense/categories/${id}`, { method: "DELETE" }),
    onSuccess: () => {
      invalidate();
      toast.success("Kategori silindi.");
    },
    onError: (mutationError) =>
      toast.error(mutationError instanceof Error ? mutationError.message : "Kategori silinemedi."),
  });

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="max-h-[85vh] overflow-y-auto sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>Kategoriler</DialogTitle>
          <DialogDescription>
            Varsayılan kategoriler plandan gelir; kaydı olan kategori silinmez, pasifleştirin.
          </DialogDescription>
        </DialogHeader>

        <div className="grid gap-2">
          {SIDES.map((sideEntry) => {
            const rows = (categories ?? []).filter((category) => category.type === sideEntry.value);
            return (
              <div key={sideEntry.value} className="grid gap-1">
                <span className="text-xs font-medium text-muted-foreground">{sideEntry.label}</span>
                <div className="grid gap-1 rounded-lg border p-2">
                  {rows.length === 0 ? (
                    <span className="px-1 py-2 text-sm text-muted-foreground">Kategori yok.</span>
                  ) : (
                    rows.map((category) => (
                      <div key={category.id} className="flex items-center justify-between gap-2 rounded px-1 py-1">
                        <div className="flex min-w-0 items-center gap-2">
                          <span className="truncate text-sm">{category.name}</span>
                          {!category.isActive && <Badge variant="outline">Pasif</Badge>}
                          <span className="text-xs text-muted-foreground">{category.recordCount} kayıt</span>
                        </div>
                        <div className="flex shrink-0 gap-1">
                          <Button
                            variant="ghost"
                            size="sm"
                            onClick={() => {
                              setEditing(category);
                              setEditName(category.name);
                              setEditActive(category.isActive);
                            }}
                          >
                            Düzenle
                          </Button>
                          {category.recordCount === 0 && (
                            <Button
                              variant="ghost"
                              size="sm"
                              className="text-destructive hover:text-destructive"
                              disabled={remove.isPending}
                              onClick={() => remove.mutate(category.id)}
                            >
                              <Trash2 className="size-4" />
                            </Button>
                          )}
                        </div>
                      </div>
                    ))
                  )}
                </div>
              </div>
            );
          })}
        </div>

        {/* Yeni kategori. */}
        <div className="grid gap-2 border-t pt-4">
          <span className="text-sm font-medium">Yeni Kategori</span>
          <div className="flex flex-wrap items-center gap-2">
            <Input
              aria-label="Kategori adı"
              placeholder="Kategori adı"
              className="w-44"
              value={name}
              onChange={(event) => setName(event.target.value)}
            />
            <Select value={side} onValueChange={(value) => setSide(value as IncomeExpenseSideDto)}>
              <SelectTrigger className="w-28" aria-label="Kategori türü">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {SIDES.map((sideEntry) => (
                  <SelectItem key={sideEntry.value} value={sideEntry.value}>
                    {sideEntry.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            <Button
              size="sm"
              disabled={create.isPending || name.trim().length === 0}
              onClick={() => create.mutate()}
            >
              {create.isPending && <Loader2 className="size-4 animate-spin" />}
              Ekle
            </Button>
          </div>
        </div>

        {/* Kategori düzenleme. */}
        <Dialog open={editing !== null} onOpenChange={(open) => !open && setEditing(null)}>
          <DialogContent className="sm:max-w-sm">
            <DialogHeader>
              <DialogTitle>Kategoriyi Düzenle</DialogTitle>
              <DialogDescription>Tür sabittir; yalnızca ad ve aktiflik değişir.</DialogDescription>
            </DialogHeader>
            <div className="grid gap-4">
              <div className="grid gap-2">
                <Label htmlFor="category-name">Ad *</Label>
                <Input
                  id="category-name"
                  value={editName}
                  onChange={(event) => setEditName(event.target.value)}
                />
              </div>
              <div className="flex items-center justify-between">
                <Label htmlFor="category-active">Aktif</Label>
                <Switch id="category-active" checked={editActive} onCheckedChange={setEditActive} />
              </div>
            </div>
            <DialogFooter>
              <Button variant="outline" onClick={() => setEditing(null)} disabled={update.isPending}>
                Vazgeç
              </Button>
              <Button
                disabled={update.isPending || editName.trim().length === 0}
                onClick={() => update.mutate()}
              >
                {update.isPending && <Loader2 className="size-4 animate-spin" />}
                Kaydet
              </Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>
      </DialogContent>
    </Dialog>
  );
}
